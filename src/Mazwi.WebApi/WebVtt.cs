using System.Globalization;
using System.Text;
using Mazwi.Application;

namespace Mazwi.WebApi;

public static class WebVtt
{
    public static string Format(IReadOnlyList<SubtitleCueDto> cues)
    {
        ArgumentNullException.ThrowIfNull(cues);
        var builder = new StringBuilder("WEBVTT\n");
        foreach (SubtitleCueDto cue in cues)
        {
            // "-->" inside cue text would end the cue early, and a blank line would end it.
            string text = cue.Text.Replace("-->", "->", StringComparison.Ordinal).Replace('\r', ' ');
            builder.Append('\n').Append(Time(cue.StartMs)).Append(" --> ").Append(Time(cue.EndMs)).Append('\n').Append(text).Append('\n');
        }

        return builder.ToString();
    }

    private static string Time(int milliseconds) =>
        TimeSpan.FromMilliseconds(milliseconds).ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
}
