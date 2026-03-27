using System.Diagnostics;

namespace QUOTA.Services;

public class AudioPlayer
{
    public void OpenMusicUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
}
