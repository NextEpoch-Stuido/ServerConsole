// StandardStreamManager.cs —— 放在主控台项目 和 Unity 项目的 Scripts 文件夹中
using System;
using System.IO;
using System.Threading;

#if UNITY_5_3_OR_NEWER
using UnityEngine;
#endif

public static class StandardStreamManager
{
    public static Action<string>? OnReceive;

    private static volatile bool _listening = false;
    private static readonly object _lock = new();

    //send message
    public static void Send(string message)
    {
        if (string.IsNullOrEmpty(message)) return;

#if UNITY_5_3_OR_NEWER
        // Unity 环境：写入 stdout（会被父进程捕获）
        Console.WriteLine(message);
#else
        // 主控台环境：写入子进程的 stdin
        if (_process != null && !_process.HasExited && _process.StandardInput.BaseStream.CanWrite)
        {
            _process.StandardInput.WriteLine(message);
            _process.StandardInput.Flush();
        }
#endif
    }

    // only for unity
    public static void StartListening()
    {
#if UNITY_5_3_OR_NEWER
        if (_listening) return;
        lock (_lock)
        {
            if (_listening) return;
            _listening = true;
        }

        // 启动后台线程读取 stdin
        var thread = new Thread(ReadStdinLoop)
        {
            IsBackground = true,
            Name = "StdinReader"
        };
        thread.Start();
#endif
    }

    // only for console
#if !UNITY_5_3_OR_NEWER
    private static System.Diagnostics.Process? _process;

    public static void AttachToProcess(System.Diagnostics.Process process)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
        _process.OutputDataReceived += (sender, args) =>
        {
            if (!string.IsNullOrEmpty(args.Data))
                OnReceive?.Invoke(args.Data);
        };
        _process.BeginOutputReadLine();
    }
#endif

#if UNITY_5_3_OR_NEWER
    private static void ReadStdinLoop()
    {
        try
        {
            string? line;
            while ((line = Console.ReadLine()) != null)
            {
                OnReceive?.Invoke(line);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[StandardStreamManager] Stdin read error: {ex}");
        }
        finally
        {
            _listening = false;
        }
    }
#endif
}