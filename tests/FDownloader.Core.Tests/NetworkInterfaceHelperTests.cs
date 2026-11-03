using FDownloader.Core.Network;
using Xunit;

namespace FDownloader.Core.Tests;

public class NetworkInterfaceHelperTests
{
    [Fact]
    public void GetAvailableInterfaces_ExcludesFilterDriversAndVirtualMiniports()
    {
        var interfaces = NetworkInterfaceHelper.GetAvailableInterfaces();

        Assert.NotEmpty(interfaces);

        // Hiçbir arayüzde antivirüs/filtre/QoS/WFP/debug çöpü olmamalıdır
        foreach (var iface in interfaces)
        {
            var text = (iface.Name + " " + iface.Description).ToLowerInvariant();
            Assert.DoesNotContain("filter", text);
            Assert.DoesNotContain("packet scheduler", text);
            Assert.DoesNotContain("lightweight", text);
            Assert.DoesNotContain("qos", text);
            Assert.DoesNotContain("kaspersky", text);
            Assert.DoesNotContain("wi-fi direct", text);
            Assert.DoesNotContain("kernel debug", text);
            Assert.DoesNotContain("6to4", text);
            Assert.DoesNotContain("ip-https", text);
        }
    }
}
