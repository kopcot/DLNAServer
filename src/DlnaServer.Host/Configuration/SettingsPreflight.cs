using System.Text.Json;
using DlnaServer.Core.Configuration;
using Microsoft.Extensions.Options;

namespace DlnaServer.Host.Configuration
{
    /// <inheritdoc cref="ISettingsPreflight"/>
    internal sealed class SettingsPreflight : ISettingsPreflight
    {
        private readonly IValidateOptions<DlnaOptions> _validator;

        public SettingsPreflight(IValidateOptions<DlnaOptions> validator)
        {
            _validator = validator;
        }

        public IReadOnlyList<string> Validate(DlnaOptions candidate)
        {
            ArgumentNullException.ThrowIfNull(candidate);

            var asStartupWouldSeeIt = Copy(candidate);

            // The same two steps Program.ConfigureOptions wires, in the same order: PostConfigure fills
            // the omissions in, and only then does the validator get a look.
            DlnaOptionsDefaults.Apply(asStartupWouldSeeIt);

            var result = _validator.Validate(name: null, asStartupWouldSeeIt);

            return result.Failed && result.Failures is not null
                ? [.. result.Failures]
                : [];
        }

        /// <remarks>
        /// A round trip through JSON rather than a hand-written clone, because a hand-written one has to
        /// be revisited every time an options class gains a member and is silently wrong until someone
        /// notices. Deliberately total: <see cref="DlnaOptionsDefaults"/> only reaches into
        /// <see cref="DlnaOptions.Library"/> today, and copying just that would make this correct by
        /// coincidence rather than by construction.
        /// <para>
        /// One difference from the original survives the trip and is harmless here: the extension map
        /// comes back with the default string comparer instead of an ordinal-ignore-case one. Nothing in
        /// the defaults or the validator looks an extension up.
        /// </para>
        /// <para>
        /// Cold by definition - once per press of Save on the Settings page.
        /// </para>
        /// </remarks>
        private static DlnaOptions Copy(DlnaOptions candidate)
        {
            var json = JsonSerializer.Serialize(candidate);

            // Null only for the JSON null literal, which Serialize emits only for a null argument - and
            // the guard above made that unreachable. Failing loudly beats validating an empty
            // DlnaOptions and reporting failures about settings the operator never touched.
            return JsonSerializer.Deserialize<DlnaOptions>(json)!;
        }
    }
}
