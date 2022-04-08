using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using static System.Threading.Tasks.Task;

namespace MakiSLBot;

internal sealed class Program
{
   public static async Task Main(string[] args)
   {
      // https://medium.com/@rainer_8955/gracefully-shutdown-c-apps-2e9711215f6d

      var host = new HostBuilder()
         .ConfigureServices((hostContext, services) => services.AddHostedService<ProgramService>())
         .UseConsoleLifetime()
         .Build();

      await host.RunAsync();
   }
}

internal class ProgramService : IHostedService
{
   private bool running;
   private Task backgroundTask;
   private MakiSLBot? makiSlBot;
   private readonly IHostApplicationLifetime applicationLifetime;
   
   public ProgramService(IHostApplicationLifetime applicationLifetime)
   {
      this.applicationLifetime = applicationLifetime;
   }
   
   public Task StartAsync(CancellationToken cancellationToken)
   {
      running = true;
      
      backgroundTask = Run(async () =>
      {
         makiSlBot = new MakiSLBot();
         while (running) await Delay(100, cancellationToken);
      }, cancellationToken);
      
      return CompletedTask;
   }

   public async Task StopAsync(CancellationToken cancellationToken)
   {
      running = false;
      await backgroundTask;
      try
      {
         makiSlBot?.Cleanup();
      }
      catch (Exception)
      {
         // ignored
      }
   }
}