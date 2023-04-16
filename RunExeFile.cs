using UnityEngine;
using System.Diagnostics;
using System.IO;

public class RunExeFile : MonoBehaviour
{
    // 用于运行 .exe 文件的方法
    public void RunExternalExe(string exePath)
    {
        if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
        {
            UnityEngine.Debug.Log("Attempting to run: " + exePath);

            string workingDirectory = Path.GetDirectoryName(exePath);

            ProcessStartInfo startInfo = new ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                CreateNoWindow = false,
                WorkingDirectory = workingDirectory
            };
            Process process = new Process { StartInfo = startInfo };
            process.Start();

            using (StreamWriter sw = process.StandardInput)
            {
                if (sw.BaseStream.CanWrite)
                {
                    sw.WriteLine($"cd \"{workingDirectory}\""); // 更改到 .exe 文件所在的目录
                    sw.WriteLine($"\"{exePath}\""); // 运行 .exe 文件
                    sw.WriteLine("pause"); // 这将使命令提示符窗口在 .exe 文件执行完成后保持打开状态，直到按下任意键。
                }
            }

            process.WaitForExit();
            UnityEngine.Debug.Log("Process completed: " + exePath);
        }
        else
        {
            UnityEngine.Debug.LogError("The .exe file path is empty, invalid or the file does not exist.");
        }
    }






    // 在 Unity 编辑器中测试运行 .exe 文件的方法
    [ContextMenu("Run Test Exe")]
    private void RunTestExe()
    {
        // 这里替换为您的 .exe 文件路径
        string exePath = @"D:\NSM Project\JsonToRpp\JsonToRpp.exe";
        RunExternalExe(exePath);
    }
}


