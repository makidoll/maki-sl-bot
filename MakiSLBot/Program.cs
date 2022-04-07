namespace MakiSLBot;

internal sealed class Program
{
   public static async Task Main(string[] args)
   {
      // https://medium.com/@rainer_8955/gracefully-shutdown-c-apps-2e9711215f6d
      
      var tcs = new TaskCompletionSource();
      var sigintReceived = false;
      
      Console.CancelKeyPress += (_, ea) =>
      {
         ea.Cancel = true;
         tcs.SetResult();
         sigintReceived = true;
      };

      AppDomain.CurrentDomain.ProcessExit += (_, _) =>
      {
         if (!sigintReceived) tcs.SetResult();
      };
      
      var makiSLBot = new MakiSLBot();

      await tcs.Task;
      
      makiSLBot.Cleanup();
   }
}