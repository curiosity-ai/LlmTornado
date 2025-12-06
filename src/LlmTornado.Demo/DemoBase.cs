using System.Diagnostics;

namespace LlmTornado.Demo;

public class DemoBase : TornadoTextFixture
{
    public static async Task DisplayImage(string base64)
    {
        
        byte[] imageBytes = Convert.FromBase64String(base64);
        string tempFile = $"{Path.GetTempFileName()}.jpg";
        await File.WriteAllBytesAsync(tempFile, imageBytes);

        if (await Helpers.ProgramExists("chafa"))
        {
            try
            {
                Process process = new Process();
                process.StartInfo.FileName = "chafa";
                process.StartInfo.Arguments = $"{tempFile}";
                process.StartInfo.UseShellExecute = false;
                process.Start();
                await process.WaitForExitAsync();
            }
            catch (Exception e)
            {
                
            }
        }
    }
}