using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public enum BattleLlmBackend
{
    TogetherGemma3nE4B = 0,
    Gemini25FlashLite = 1,
}

public static class BattleOrdersHttpClient
{
    [Serializable]
    private sealed class OrderRequestDto
    {
        public string backendId;
        public string systemInstruction;
        public string userPayloadJson;
    }

    [Serializable]
    private sealed class OrderResponseDto
    {
        public string backendId;
        public string provider;
        public string model;
        public string text;
    }

    public static IEnumerator PostCommand(
        string url,
        string appSharedToken,
        string backendId,
        string systemInstruction,
        string userPayloadJson,
        int timeoutSeconds,
        Action<string, string, string, string> onSuccess,
        Action<string> onError)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            onError?.Invoke("Proxy URL is empty.");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(appSharedToken))
        {
            onError?.Invoke("App shared token is empty.");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(backendId))
        {
            onError?.Invoke("BackendId is empty.");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(systemInstruction))
        {
            onError?.Invoke("SystemInstruction is empty.");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(userPayloadJson))
        {
            onError?.Invoke("UserPayloadJson is empty.");
            yield break;
        }

        OrderRequestDto requestDto = new OrderRequestDto
        {
            backendId = backendId,
            systemInstruction = systemInstruction,
            userPayloadJson = userPayloadJson
        };

        string requestJson = JsonUtility.ToJson(requestDto);
        byte[] requestBody = Encoding.UTF8.GetBytes(requestJson);

        using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(requestBody);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = Mathf.Max(1, timeoutSeconds);

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("X-App-Token", appSharedToken);

            yield return request.SendWebRequest();

            string responseBody = request.downloadHandler != null
                ? request.downloadHandler.text
                : string.Empty;

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(BuildErrorMessage(request.responseCode, request.error, responseBody));
                yield break;
            }

            if (string.IsNullOrWhiteSpace(responseBody))
            {
                onError?.Invoke("Server returned an empty response body.");
                yield break;
            }

            OrderResponseDto responseDto = JsonUtility.FromJson<OrderResponseDto>(responseBody);
            if (responseDto == null || string.IsNullOrWhiteSpace(responseDto.text))
            {
                onError?.Invoke($"Response JSON does not contain a valid text field. Raw={responseBody}");
                yield break;
            }

            onSuccess?.Invoke(
                responseDto.backendId,
                responseDto.provider,
                responseDto.model,
                responseDto.text);
        }
    }

    private static string BuildErrorMessage(long statusCode, string unityError, string responseBody)
    {
        string statusText = statusCode > 0
            ? $"Status={statusCode}"
            : "Status=NoResponse";

        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return $"{statusText}, Error={unityError}";
        }

        return $"{statusText}, Error={unityError}, Body={responseBody}";
    }
}