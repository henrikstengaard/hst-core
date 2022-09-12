namespace Hst.Core.Tests.ExtensionTests;

using Extensions;
using Xunit;

public class GivenToSectorSizeExtension
{
    [Fact]
    public void WhenConvertValidDiskSizeThenDiskSizeIsNotChanged()
    {
        // arrange - disk size of 1024 bytes
        long diskSize = 1024;

        // act - convert to sector size
        diskSize = diskSize.ToSectorSize();
        
        // assert - disk size is 1024 bytes
        Assert.Equal(1024, diskSize);
    }
    
    [Fact]
    public void WhenConvertDiskSizeIsLargerThanSectorSizeThenDiskSizeIsReducedToNearestSector()
    {
        // arrange - disk size 1000 bytes larger than sector size of 512 bytes
        long diskSize = 1000;

        // act - convert to sector size
        diskSize = diskSize.ToSectorSize();
        
        // assert - disk size is reduced to 512 bytes
        Assert.Equal(512, diskSize);
    }
}