# Performance

The decisions taken for the machine this runs on - a NAS with mechanical drives
and limited memory - and what each one costs.

## The served-bytes cache

Its purpose is acoustic, not throughput: the NAS drives are mechanical and audible in the room, so a file
already sent should not wake a spun-down disc to be read again.

**It is sized against the machine, not against a constant.** `FileCache.MaxTotalSizeInMegabytes` (1 GB) is
additionally clamped to **half** of what the machine reports, and `MaxFileSizeInMegabytes` (512 MB) to half
of whatever budget that yields. Production servers have 2-4 GB in total, so on the smallest supported
machine the clamp and the configured value agree at 1 GB rather than one silently overriding the other.
This is a deliberately generous share: the acoustic goal is what the memory buys, and a spun-up drive is
the cost of being frugal here.

The startup line reports all three figures - configured, available and resolved - because the resolved
budget is not always the configured one, and without the comparison a clamped value looks like a bug:

```
Served-bytes cache holds up to 1024 MB (configured 1024 MB, machine reports 39907 MB)
```

A file above the per-file cap is never read into memory at all; it streams from the disc with range
support. `IServedFileCache` exposes `BudgetInBytes` and `MaxFileSizeInBytes` so the resolved values are
observable rather than inferred.

A cached film is one contiguous 512 MB array on the large object heap, and that churn is what took the
working set to 5073 MB on a box reporting 40 GB free, where the GC had no reason to compact. The budget and
the per-file share bound how many such payloads can be held at once; whether that is enough on a 2 GB
machine is a question for a measured constrained run, not for a default - see `PLAN.md` section 3b.

**Payloads are `ReadOnlyMemory<byte>` and are served with `AsStream()`** from
`CommunityToolkit.HighPerformance`. There is no `File(ReadOnlyMemory<byte>, …)` overload, and the
alternative — `ToArray()` — would copy the entire payload on every range request; a renderer issues one of
those per range. The cache therefore hands out a view over its own buffer, which is also what leaves it
free to hold a slice of a larger buffer later without changing a single caller.

**Concurrent callers for one path share one read.** The first starts it, the rest await the same operation,
so 32 renderers asking for one thumbnail at the same moment cost one disc read and one allocation instead
of 32 of each — measured, and the reason it matters on a 2-4 GB box is that duplicate payloads multiply
the transient peak. The shared read deliberately ignores the individual caller's `CancellationToken`, since
one request going away must not abandon the read the others are waiting on.

**What it holds is observable.** `GET /manage/filecache` reports the resolved budget, bytes held, entry
count, hit and miss counts, reads in flight, and **every cached path** - the last of these being the point,
because a byte total cannot distinguish a cache doing its job from a leak. `GET /manage/filecache/clear`
drops every payload and forces one compacting collection: cached payloads above 85 KB live on the large
object heap, which is reclaimed only on a Gen2 collection and never compacted by default, so clearing
without collecting would report success and hand nothing back to the operating system. That is the only
place in the application that forces a collection, and only ever on an operator's request.

Do **not** pool these payloads. Handing MVC a stream over a rented buffer would let eviction return that
buffer to the pool while a response is still writing it; GC-owned memory cannot fail that way, because the
stream holds a reference.
