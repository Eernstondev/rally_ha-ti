using System;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace RallyAyiti.EditorTools
{
    /// <summary>
    /// Copie les erreurs, avertissements et messages « Rally Ayiti » de la console dans Logs/rally.log,
    /// ainsi que les erreurs de compilation : pratique pour diagnostiquer le projet à distance.
    /// </summary>
    [InitializeOnLoad]
    public static class RallyLog
    {
        private static readonly string LogPath = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "rally.log");

        static RallyLog()
        {
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            CompilationPipeline.assemblyCompilationFinished -= OnCompiled;
            CompilationPipeline.assemblyCompilationFinished += OnCompiled;
            Write("Editeur pret (" + Application.unityVersion + ")");
        }

        private static void OnCompiled(string assembly, CompilerMessage[] messages)
        {
            foreach (CompilerMessage m in messages)
                if (m.type == CompilerMessageType.Error) Write($"COMPILE ERROR {m.file}:{m.line} {m.message}");
        }

        private static void OnLog(string message, string stack, LogType type)
        {
            bool ours = message.StartsWith("Rally Ayiti");
            if (type == LogType.Log && !ours) return;
            string first = stack?.Split('\n')[0] ?? "";
            Write($"{type}: {message} {(type == LogType.Log ? "" : first)}");
        }

        private static void Write(string line)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss} {line}{Environment.NewLine}");
            }
            catch (IOException)
            {
                // Journal facultatif : on ne bloque jamais l'éditeur pour lui.
            }
        }
    }
}
