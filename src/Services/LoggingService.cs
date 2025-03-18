
namespace PocketStoryPoker.Services
{
    public interface ILoggingService
    {
        void LogInfo(string message);
        void LogError(string message);
    }

    public class LoggingService : ILoggingService
    {
#if DEBUG
        public void LogInfo(string message) => Console.WriteLine(message);
        public void LogError(string message) => Console.WriteLine($"Error: {message}");
#else
        public void LogInfo(string message) { }
        public void LogError(string message) { }
#endif
    }
}
