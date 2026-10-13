using Microsoft.Extensions.Logging;

namespace HexMaster.Battleship.Core.Observability;

// Log messages of the shared kernel; event ids 5000-5999.
internal static partial class CoreLog
{
    [LoggerMessage(EventId = 5000, Level = LogLevel.Warning, Message = "Use case {UseCase} failed with {ExceptionType}")]
    public static partial void UseCaseFailed(ILogger logger, string useCase, string exceptionType);
}
