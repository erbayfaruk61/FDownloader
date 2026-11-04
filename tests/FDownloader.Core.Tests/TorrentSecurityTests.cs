using System.Net;
using FDownloader.Core.Torrent;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FDownloader.Core.Tests;

public class TorrentSecurityTests : IDisposable
{
    private readonly string _tempDir;

    public TorrentSecurityTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "fdownloader_torrentsec_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch { }
    }

    [Theory]
    [InlineData("normal_file.mp4", true)]
    [InlineData("subfolder/file.mkv", true)]
    [InlineData("season 1/episode 01.avi", true)]
    [InlineData("../outside.exe", false)]
    [InlineData("../../windows/system32/cmd.exe", false)]
    [InlineData("folder/../../../secret.txt", false)]
    [InlineData("C:/Windows/System32/malicious.dll", false)]
    [InlineData("/etc/passwd", false)]
    [InlineData("\\\\evil-server\\share\\exploit.exe", false)]
    public void ValidateTorrentFilePaths_BlocksPathTraversalAndRootedPaths(string torrentRelativePath, bool expected)
    {
        var files = new[] { torrentRelativePath };
        bool isValid = TorrentSecurityValidator.ValidateTorrentFilePaths(_tempDir, files, out var unsafePath);

        Assert.Equal(expected, isValid);
        if (!expected)
        {
            Assert.Equal(torrentRelativePath, unsafePath);
        }
    }

    [Fact]
    public void EvaluateNetworkBinding_WhenNoInterfaceConfigured_BindsAnyAddressWithoutKillSwitch()
    {
        var (bindIp, isKillSwitchActive) = TorrentSecurityValidator.EvaluateNetworkBinding(
            networkInterfaceId: null,
            logger: NullLogger.Instance);

        Assert.Equal(IPAddress.Any, bindIp);
        Assert.False(isKillSwitchActive);
    }

    [Fact]
    public void EvaluateNetworkBinding_WhenVpnInterfaceMissingOrDown_EnforcesLoopbackKillSwitch()
    {
        // Var olmayan bir hayali VPN adaptör GUID'i verelim
        const string fakeVpnGuid = "{00000000-0000-0000-0000-000000000000}";

        var (bindIp, isKillSwitchActive) = TorrentSecurityValidator.EvaluateNetworkBinding(
            networkInterfaceId: fakeVpnGuid,
            logger: NullLogger.Instance);

        // Gerçek IP sızıntısını önlemek için trafik Loopback (127.0.0.1) adresine kilitlenmeli
        Assert.Equal(IPAddress.Loopback, bindIp);
        Assert.True(isKillSwitchActive);
    }
}
