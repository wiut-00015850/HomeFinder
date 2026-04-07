using System.Collections.Generic;

namespace HomeFinder.Models
{
    public class MapPointDto
    {
        public decimal Lat { get; set; }
        public decimal Lng { get; set; }
    }

    public class MapAreaRequest
    {
        public List<MapPointDto> Polygon { get; set; } = new();
    }
}

