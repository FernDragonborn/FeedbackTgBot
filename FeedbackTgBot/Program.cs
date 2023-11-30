#define DEBUG

using log4net;

[assembly: log4net.Config.XmlConfigurator]

namespace FeedbackTgBot;

internal class Program
{
    static readonly ILog log = LogManager.GetLogger(typeof(Program));

    static async Task Main()
    {
        Console.OutputEncoding = System.Text.Encoding.Unicode;
        Console.InputEncoding = System.Text.Encoding.Unicode;

        Console.WriteLine("started. If no messages after, logger haven't initialized");
        log.Info("logger initialized");
        Console.WriteLine($"is debug enabled: {log.IsDebugEnabled}");
        Console.WriteLine($"is info enabled: {log.IsInfoEnabled}");
        Console.WriteLine($"is warn enabled: {log.IsWarnEnabled}");
        Console.WriteLine($"is error enabled: {log.IsErrorEnabled}");
        Console.WriteLine($"is fatal enabled: {log.IsFatalEnabled}");

        string dotEnv = File.ReadAllText(".env");
        DotNetEnv.Env.Load(".env");

        //DB must be first for FindFirstFreeRow()
        Crud.DbInit();
        var tg = new Telegram();
        await tg.Init();

        while (true)
        {
            Thread.Sleep(5);
        }

    }
}

