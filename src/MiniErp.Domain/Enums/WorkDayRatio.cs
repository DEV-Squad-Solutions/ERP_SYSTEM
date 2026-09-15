using System;
using System.Collections.Generic;
using System.Text;

namespace MiniErp.Domain.Enums
{
    public enum WorkDayRatio
    {
        OneDay = 1,           // 1
        FullDay = OneDay,     // Alias for OneDay
        TwoDays = 2,             // 2.0
        ThreeDays = 3,           // 3.0
        FourDays = 4,            // 4.0
        FiveDays = 5,          // 5.0
        ThreeQuarterDay = 6,   // 0.75
        TwoThirdsDay = 7,       // 0.66
        HalfDay = 8,           // 0.50
        ThirdDay = 9,          // 0.33
        QuarterDay = 10,        // 0.25

    }
}
