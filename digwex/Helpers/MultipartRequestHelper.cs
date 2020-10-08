﻿using Microsoft.Net.Http.Headers;
using System;
using System.IO;

namespace Digwex.Helpers
{
  public class MultipartRequestHelper
  {

    public static string GetBoundary(MediaTypeHeaderValue contentType, int lengthLimit)
    {
      string boundary = HeaderUtilities.RemoveQuotes(contentType.Boundary).ToString();
      if (string.IsNullOrWhiteSpace(boundary)) {
        throw new InvalidDataException("Missing content-type boundary.");
      }

      if (boundary.Length > lengthLimit) {
        throw new InvalidDataException(
            $"Multipart boundary length limit {lengthLimit} exceeded.");
      }

      return boundary;
    }

    public static bool IsMultipartContentType(string contentType)
    {
      return !string.IsNullOrEmpty(contentType)
             && contentType.IndexOf("multipart/", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static bool HasFileContentDisposition(ContentDispositionHeaderValue contentDisposition)
    {
      // Content-Disposition: form-data; name="myfile1"; filename="Misc 002.jpg"
      return contentDisposition != null
             && contentDisposition.DispositionType.Equals("form-data")
             && (!string.IsNullOrEmpty(contentDisposition.FileName.ToString())
                 || !string.IsNullOrEmpty(contentDisposition.FileNameStar.ToString()));
    }
  }
}
