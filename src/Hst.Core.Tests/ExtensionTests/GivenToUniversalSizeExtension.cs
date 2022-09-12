namespace Hst.Core.Tests.ExtensionTests;

using System;
using Extensions;
using Xunit;

public class GivenToUniversalSizeExtension
{
    [Fact]
    public void WhenConvertToUniversalSizeThenDiskSizeIsReducedBy5Percent()
    {
        // arrange - disk size of 1 mb
        long diskSize = 1024 * 1024;

        // act - convert to universal size
        diskSize = diskSize.ToUniversalSize();
        
        // assert - disk size is reduced by 5%
        Assert.Equal(Convert.ToInt64(1024 * 1024 * 0.95), diskSize);
    }
}