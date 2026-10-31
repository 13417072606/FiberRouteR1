using System.Globalization;

namespace FiberRoute.Core;

public static class UpdateReminderPolicy
{
    public static bool ShouldCheck(DateTime localNow,string? lastAttemptDay)
        =>lastAttemptDay!=localNow.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);

    public static bool HasPending(string? programVersion,string? dataVersion,string currentProgram,string? currentData)
        =>programVersion is {Length:>0}&&Updates.IsNewProgram(programVersion,currentProgram)
            ||dataVersion is {Length:>0}&&dataVersion!=currentData;
}
