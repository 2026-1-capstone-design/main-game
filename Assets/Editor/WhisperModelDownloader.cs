// Unity Editor에서 Whisper 모델 파일을 다운로드한다.
// 대상 경로는 Assets/StreamingAssets/Whisper 이다.
// 이미 같은 파일이 있으면 다시 받지 않는다.
// 다운로드 후 AssetDatabase를 갱신한다.
// 빌드 런타임에서는 실행되지 않는다.

#if UNITY_EDITOR
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class WhisperModelDownloader
{
    private const string ModelFileName = "ggml-large-v3-turbo-q8_0.bin";
    private const string DownloadUrl =
        "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3-turbo-q8_0.bin?download=true";

    [MenuItem("Tools/Whisper/Download large-v3-turbo q8_0")]
    public static async void DownloadModel()
    {
        string targetDirectory = Path.Combine(Application.dataPath, "StreamingAssets", "Whisper");
        string targetPath = Path.Combine(targetDirectory, ModelFileName);

        if (File.Exists(targetPath))
        {
            Debug.Log($"[WhisperModelDownloader] Model already exists: {targetPath}");
            return;
        }

        Directory.CreateDirectory(targetDirectory);

        try
        {
            EditorUtility.DisplayProgressBar("Whisper Model Download", "Starting download...", 0f);

            using HttpClient client = new HttpClient();
            using HttpResponseMessage response = await client.GetAsync(
                DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead
            );

            response.EnsureSuccessStatusCode();

            long? totalBytes = response.Content.Headers.ContentLength;

            await using Stream remoteStream = await response.Content.ReadAsStreamAsync();
            await using FileStream fileStream = new FileStream(
                targetPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                1024 * 1024,
                true
            );

            byte[] buffer = new byte[1024 * 1024];
            long downloadedBytes = 0;

            while (true)
            {
                int readBytes = await remoteStream.ReadAsync(buffer, 0, buffer.Length);
                if (readBytes <= 0)
                    break;

                await fileStream.WriteAsync(buffer, 0, readBytes);
                downloadedBytes += readBytes;

                float progress = totalBytes.HasValue && totalBytes.Value > 0
                    ? (float)downloadedBytes / totalBytes.Value
                    : 0f;

                EditorUtility.DisplayProgressBar(
                    "Whisper Model Download",
                    $"{downloadedBytes / 1024 / 1024} MB downloaded",
                    progress
                );
            }

            Debug.Log($"[WhisperModelDownloader] Download completed: {targetPath}");
            AssetDatabase.Refresh();
        }
        catch (Exception exception)
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }

            Debug.LogError($"[WhisperModelDownloader] Download failed: {exception}");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }
}
#endif
