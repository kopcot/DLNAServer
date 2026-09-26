using System.Text;
using DlnaServer.Core.Configuration;
using DlnaServer.Core.Hosting;
using DlnaServer.Core.Uploads;
using DlnaServer.Host.Controllers;
using DlnaServer.Host.Uploads;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace DlnaServer.IntegrationTests
{
    /// <summary>
    /// Covers how the upload endpoint reads a multipart post and what it leaves on disc when a post goes
    /// wrong part-way.
    /// </summary>
    [TestFixture]
    internal sealed class UploadControllerTest
    {
        private const string Boundary = "----upload-test-boundary";

        private string _destination = string.Empty;
        private UploadReportStore _reports = null!;
        private LibraryScanSignal _scanSignal = null!;

        [SetUp]
        public void SetUp()
        {
            _destination = Directory.CreateTempSubdirectory("dlna-upload-").FullName;
            _reports = new UploadReportStore(TimeProvider.System);
            _scanSignal = new LibraryScanSignal();
        }

        [TearDown]
        public void TearDown()
        {
            _scanSignal.Dispose();
            Directory.Delete(_destination, recursive: true);
        }

        [Test]
        public async Task Upload_WithAFormFieldPastTheLimit_IsRefused()
        {
            // Arrange
            var body = Body(
                Field(name: "root", value: _destination),
                Field(name: "existing", value: new string('a', 5 * 1024)),
                FilePart(fileName: "film.mp4", content: new byte[] { 1, 2, 3 }));
            var controller = CreateController(body: new MemoryStream(body), devices: new FakeUploadDeviceRepository());

            // Act
            var result = await controller.Upload(cancellationToken: CancellationToken.None);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>(
                "because the request body is uncapped for the files, so a field has to be bounded while it is "
                + "read or one huge field is read into memory whole");
        }

        [Test]
        public async Task Upload_WithALargeFieldTheFormDoesNotHave_IgnoresItAndTakesTheFile()
        {
            // Arrange
            var body = Body(
                Field(name: "comment", value: new string('a', 64 * 1024)),
                Field(name: "root", value: _destination),
                FilePart(fileName: "film.mp4", content: new byte[] { 1, 2, 3 }));
            var controller = CreateController(body: new MemoryStream(body), devices: new FakeUploadDeviceRepository());

            // Act
            var result = await controller.Upload(cancellationToken: CancellationToken.None);

            // Assert
            result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(StatusCodes.Status303SeeOther,
                "because a field this form does not have is skipped without being read, not refused");
            File.Exists(Path.Combine(_destination, "film.mp4")).Should().BeTrue(
                "because the file after the unknown field is still an ordinary upload");
        }

        [Test]
        public async Task Upload_WithMorePartsThanTheCap_IsRefused()
        {
            // Arrange
            // Otherwise a valid upload, so nothing but the count can be what refuses it.
            var parts = new byte[1002][];
            parts[0] = Field(name: "root", value: _destination);

            for (var index = 1; index < parts.Length - 1; index++)
            {
                parts[index] = Field(name: "comment", value: string.Empty);
            }

            parts[^1] = FilePart(fileName: "film.mp4", content: new byte[] { 1, 2, 3 });

            var controller = CreateController(
                body: new MemoryStream(Body(parts)),
                devices: new FakeUploadDeviceRepository());

            // Act
            var result = await controller.Upload(cancellationToken: CancellationToken.None);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>(
                "because every part costs memory even when it is empty, so the count per post is capped");
        }

        [Test]
        public async Task Upload_LeavesAPartialFileOfTheSameNameAlone()
        {
            // Arrange
            var othersPartial = Path.Combine(_destination, "film.mp4.uploading");
            await File.WriteAllTextAsync(othersPartial, "still arriving", CancellationToken.None);

            var body = Body(
                Field(name: "root", value: _destination),
                FilePart(fileName: "film.mp4", content: new byte[] { 1, 2, 3 }));
            var controller = CreateController(body: new MemoryStream(body), devices: new FakeUploadDeviceRepository());

            // Act
            _ = await controller.Upload(cancellationToken: CancellationToken.None);

            // Assert
            File.Exists(othersPartial).Should().BeTrue(
                "because that file may be another upload of the same name still copying, and deleting it "
                + "would break that upload");
            File.Exists(Path.Combine(_destination, "film.mp4")).Should().BeTrue(
                "because this upload writes to a partial name of its own and still lands in place");
        }

        [Test]
        public async Task Upload_RemovesALongIdlePartialOfTheSameNameOnly()
        {
            // Arrange - one left by a crashed upload of this name, one of another name.
            var abandoned = Path.Combine(_destination, $"film.mp4.{Guid.NewGuid():N}.uploading");
            var otherName = Path.Combine(_destination, $"other.mp4.{Guid.NewGuid():N}.uploading");
            var longAgo = DateTime.UtcNow.AddHours(-2);

            foreach (var path in new[] { abandoned, otherName })
            {
                await File.WriteAllTextAsync(path, "half", CancellationToken.None);
                File.SetLastWriteTimeUtc(path, longAgo);
            }

            var body = Body(
                Field(name: "root", value: _destination),
                FilePart(fileName: "film.mp4", content: new byte[] { 1, 2, 3 }));
            var controller = CreateController(body: new MemoryStream(body), devices: new FakeUploadDeviceRepository());

            // Act
            _ = await controller.Upload(cancellationToken: CancellationToken.None);

            // Assert
            File.Exists(abandoned).Should().BeFalse(
                "because an hour-idle partial of this name was left by an upload that died with the process");
            File.Exists(otherName).Should().BeTrue(
                "because only partials of the file just uploaded are tidied, never another file's");
        }

        [Test]
        public async Task Upload_LeavesAPartialOfALongerNameAlone()
        {
            // Arrange - film.mp4.mp4 is another file, whose partial merely starts with this upload's name.
            var longerName = Path.Combine(_destination, $"film.mp4.mp4.{Guid.NewGuid():N}.uploading");
            await File.WriteAllTextAsync(longerName, "half", CancellationToken.None);
            File.SetLastWriteTimeUtc(longerName, DateTime.UtcNow.AddHours(-2));

            var body = Body(
                Field(name: "root", value: _destination),
                FilePart(fileName: "film.mp4", content: new byte[] { 1, 2, 3 }));
            var controller = CreateController(body: new MemoryStream(body), devices: new FakeUploadDeviceRepository());

            // Act
            _ = await controller.Upload(cancellationToken: CancellationToken.None);

            // Assert
            File.Exists(longerName).Should().BeTrue(
                "because only a partial of exactly the uploaded name is tidied, and film.mp4.mp4 is a different file");
        }

        [Test]
        public async Task Upload_WithMorePartsThanTheCapAfterAFileWasWritten_ReportsTheFileAndStillScans()
        {
            // Arrange
            var parts = new byte[1002][];
            parts[0] = Field(name: "root", value: _destination);
            parts[1] = FilePart(fileName: "film.mp4", content: new byte[] { 1, 2, 3 });

            for (var index = 2; index < parts.Length; index++)
            {
                parts[index] = Field(name: "comment", value: string.Empty);
            }

            var devices = new FakeUploadDeviceRepository();
            var controller = CreateController(body: new MemoryStream(Body(parts)), devices: devices);

            // Act
            var result = await controller.Upload(cancellationToken: CancellationToken.None);

            // Assert
            await AssertFinishedAfterStoppingAsync(result: result, controller: controller, devices: devices);
        }

        [Test]
        public async Task Upload_WithAFormFieldPastTheLimitAfterAFileWasWritten_ReportsTheFileAndStillScans()
        {
            // Arrange
            var body = Body(
                Field(name: "root", value: _destination),
                FilePart(fileName: "film.mp4", content: new byte[] { 1, 2, 3 }),
                Field(name: "existing", value: new string('a', 5 * 1024)));
            var devices = new FakeUploadDeviceRepository();
            var controller = CreateController(body: new MemoryStream(body), devices: devices);

            // Act
            var result = await controller.Upload(cancellationToken: CancellationToken.None);

            // Assert
            await AssertFinishedAfterStoppingAsync(result: result, controller: controller, devices: devices);
        }

        [Test]
        public async Task Upload_WhenAFileOfThatNameArrivesMidCopy_AndExistingFilesAreKept_SkipsIt()
        {
            // Arrange
            var target = Path.Combine(_destination, "film.mp4");
            var body = Body(
                Field(name: "root", value: _destination),
                Field(name: "existing", value: "skip"),
                FilePart(fileName: "film.mp4", content: new byte[256 * 1024]));

            var stream = new TriggeredReadStream(
                content: body,
                triggerAt: body.Length / 2,
                onTrigger: () => File.WriteAllText(target, "theirs"));
            var controller = CreateController(body: stream, devices: new FakeUploadDeviceRepository());

            // Act
            _ = await controller.Upload(cancellationToken: CancellationToken.None);

            // Assert
            (await File.ReadAllTextAsync(target, CancellationToken.None)).Should().Be("theirs",
                "because the operator asked for existing files to be kept, and one arrived before this "
                + "upload was moved into place");
            ReportFor(controller).Files.Should().ContainSingle().Which.Outcome.Should().Be(UploadOutcome.Skipped,
                "because the report has to say what happened to the file, which is that it was not written");
            Directory.GetFiles(_destination, "*.uploading").Should().BeEmpty(
                "because the copy that was not moved into place must not be left in the media folder");
        }

        [Test]
        public async Task Upload_WhenTheClientGoesAwayMidCopy_LeavesNoPartialFile()
        {
            // Arrange
            var body = Body(
                Field(name: "root", value: _destination),
                FilePart(fileName: "film.mp4", content: new byte[256 * 1024]));

            var stream = new TriggeredReadStream(
                content: body,
                triggerAt: body.Length / 2,
                onTrigger: static () => throw new OperationCanceledException());
            var controller = CreateController(body: stream, devices: new FakeUploadDeviceRepository());

            // Act
            var act = () => controller.Upload(cancellationToken: CancellationToken.None);

            // Assert
            _ = await act.Should().ThrowAsync<OperationCanceledException>(
                "because a dropped connection is not a write failure and is not reported as one");
            Directory.GetFiles(_destination).Should().BeEmpty(
                "because a half-written file left in the media folder would sit there for ever");
        }

        [Test]
        public async Task Upload_WhenTheDeviceCannotBeRemembered_StillRequestsAScan()
        {
            // Arrange
            var body = Body(
                Field(name: "root", value: _destination),
                FilePart(fileName: "film.mp4", content: new byte[] { 1, 2, 3 }));
            var devices = new FakeUploadDeviceRepository { FailWith = new InvalidOperationException("down") };
            var controller = CreateController(body: new MemoryStream(body), devices: devices);

            // Act
            var act = () => controller.Upload(cancellationToken: CancellationToken.None);

            // Assert
            _ = await act.Should().ThrowAsync<InvalidOperationException>(
                "because only a database error is swallowed there, and this arrangement fails otherwise");
            (await _scanSignal.WaitForRequestAsync(timeout: TimeSpan.Zero, cancellationToken: CancellationToken.None))
                .Should().BeTrue(
                    "because the file is already on disc, so the scan that indexes it must not depend on "
                    + "remembering the device succeeding");
        }

        private UploadController CreateController(Stream body, FakeUploadDeviceRepository devices)
        {
            var options = new DlnaOptions
            {
                Library = new LibraryOptions
                {
                    SourceFolders = [_destination],
                },
            };

            var httpContext = new DefaultHttpContext();
            httpContext.Request.ContentType = $"multipart/form-data; boundary={Boundary}";
            httpContext.Request.Body = body;

            return new UploadController(
                new StaticOptionsMonitor<DlnaOptions>(options),
                _reports,
                devices,
                _scanSignal,
                new UploadSecurityLog(NullLogger<UploadSecurityLog>.Instance),
                TimeProvider.System,
                NullLogger<UploadController>.Instance)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = httpContext,
                },
            };
        }

        // The shared outcome of a post refused part-way through, once a file had already been moved into place.
        private async Task AssertFinishedAfterStoppingAsync(
            IActionResult result,
            UploadController controller,
            FakeUploadDeviceRepository devices)
        {
            result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(StatusCodes.Status303SeeOther,
                "because a file is already in place, so the post finishes with its report rather than a bare 400");
            File.Exists(Path.Combine(_destination, "film.mp4")).Should().BeTrue(
                "because the file before the refused part was written before the refusal");

            var files = ReportFor(controller).Files;
            files.Should().HaveCount(2, "because the report lists the written file and the point the post was refused at");
            files[0].Outcome.Should().Be(UploadOutcome.Uploaded, "because the first file was written whole");
            files[1].Outcome.Should().Be(UploadOutcome.Failed,
                "because the rest of the post was refused, and the report has to say so");

            (await _scanSignal.WaitForRequestAsync(timeout: TimeSpan.Zero, cancellationToken: CancellationToken.None))
                .Should().BeTrue("because the written file is on disc and must be indexed like any other");
            devices.Recorded.Should().ContainSingle(
                    "because the destination is remembered for a post that wrote something")
                .Which.FileCount.Should().Be(1, "because one file was written");
        }

        private UploadReport ReportFor(UploadController controller)
        {
            var location = controller.Response.Headers.Location.ToString();
            var id = Guid.Parse(location[(location.IndexOf('=', StringComparison.Ordinal) + 1)..]);

            return _reports.Find(id) ?? throw new InvalidOperationException("No report was stored.");
        }

        private static byte[] Field(string name, string value)
        {
            return Encoding.UTF8.GetBytes(
                $"--{Boundary}\r\nContent-Disposition: form-data; name=\"{name}\"\r\n\r\n{value}\r\n");
        }

        private static byte[] FilePart(string fileName, byte[] content)
        {
            var header = Encoding.UTF8.GetBytes(
                $"--{Boundary}\r\nContent-Disposition: form-data; name=\"files\"; filename=\"{fileName}\"\r\n"
                + "Content-Type: application/octet-stream\r\n\r\n");

            return [.. header, .. content, .. "\r\n"u8];
        }

        private static byte[] Body(params byte[][] parts)
        {
            using var body = new MemoryStream();

            foreach (var part in parts)
            {
                body.Write(part);
            }

            body.Write(Encoding.UTF8.GetBytes($"--{Boundary}--\r\n"));

            return body.ToArray();
        }
    }
}
