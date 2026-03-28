using System;
using UnityEngine;

public class OpenLinkButton : MonoBehaviour
{
    [SerializeField] private string url = "hackaton3.html";

    public void OpenUrl()
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            Debug.LogWarning("OpenLinkButton: URL is empty.");
            return;
        }

        string finalUrl = BuildUrl(url);
        Application.OpenURL(finalUrl);
    }

    private static string BuildUrl(string rawUrl)
    {
        if (rawUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            rawUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            rawUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
            rawUrl.StartsWith("jar:", StringComparison.OrdinalIgnoreCase))
        {
            return rawUrl;
        }

        string basePath = Application.streamingAssetsPath.Replace('\\', '/');
        string relativePath = rawUrl.TrimStart('/', '\\');

        if (!basePath.EndsWith("/"))
        {
            basePath += "/";
        }

        string combinedPath = basePath + relativePath;

        // If StreamingAssets path is a normal filesystem path, add file://
        if (!combinedPath.Contains("://"))
        {
            combinedPath = "file://" + combinedPath;
        }

        return combinedPath;
    }
}