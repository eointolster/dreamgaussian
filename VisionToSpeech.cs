//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

using System;
using System.Collections;
using System.Text; // This line is necessary for UTF8Encoding
using UnityEngine;
using UnityEngine.Networking;
using System.Runtime.InteropServices;
using System.Diagnostics;

public class VisionToSpeech : MonoBehaviour
{

    public KeyCode activationKey = KeyCode.Keypad7;
    public Camera playerCamera; // Assign this from the inspector or find the camera dynamically
    private string openAIApiKey = "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"; // put in your own api key please

    void Start()
    {
        openAIApiKey = "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"; // TODO: Retrieve the OpenAI API key securely
        // If the camera isn't set in the inspector, try to find it at runtime
        if (playerCamera == null)
        {
            playerCamera = Camera.main; // Assumes the main camera is the player's camera
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(activationKey))
        {
            StartCoroutine(CaptureAndProcessImage());
        }
    }

    private IEnumerator CaptureAndProcessImage()
    {
        // Wait until the end of the frame so everything is rendered
        yield return new WaitForEndOfFrame();

        // If the camera doesn't have an assigned targetTexture, create one
        if (playerCamera.targetTexture == null)
        {
            playerCamera.targetTexture = new RenderTexture(Screen.width, Screen.height, 24);
        }

        // Create a RenderTexture with the same dimensions as the camera's target texture
        RenderTexture renderTexture = new RenderTexture(playerCamera.targetTexture.width, playerCamera.targetTexture.height, 24);

        // Set the camera's targetTexture to the temporary RenderTexture
        RenderTexture currentRT = RenderTexture.active;
        RenderTexture.active = renderTexture;
        playerCamera.targetTexture = renderTexture;

        // Render the camera's view to the RenderTexture
        playerCamera.Render();

        // Copy the RenderTexture contents to a new Texture2D
        Texture2D cameraImage = new Texture2D(renderTexture.width, renderTexture.height);
        cameraImage.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        cameraImage.Apply();

        // Reset the camera's targetTexture and active RenderTexture
        playerCamera.targetTexture = null;
        RenderTexture.active = currentRT;

        // Convert the captured image to a byte array, then encode as Base64
        byte[] imageBytes = cameraImage.EncodeToPNG();
        string imageBase64 = Convert.ToBase64String(imageBytes);

        // Clean up the textures
        Destroy(renderTexture);
        Destroy(cameraImage);

        // Proceed to send this to OpenAI
        yield return StartCoroutine(SendImageToOpenAI(imageBase64));
    }


    private IEnumerator SendImageToOpenAI(string base64Image)
    {
        string openAIEndpoint = "https://api.openai.com/v1/chat/completions";
        string jsonPayload = "{\"model\":\"gpt-4-vision-preview\",\"messages\":[{\"role\":\"user\",\"content\":[{\"type\":\"text\",\"text\":\"What’s in this image?\"},{\"type\":\"image_url\",\"image_url\":{\"url\":\"data:image/jpeg;base64," + base64Image + "\"}}]}],\"max_tokens\":300}";

        using (UnityWebRequest webRequest = UnityWebRequest.PostWwwForm(openAIEndpoint, ""))
        {
            byte[] jsonToSend = new UTF8Encoding().GetBytes(jsonPayload);
            webRequest.uploadHandler = (UploadHandler)new UploadHandlerRaw(jsonToSend);
            webRequest.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("Authorization", "Bearer " + openAIApiKey);

            yield return webRequest.SendWebRequest();
            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogError($"Error sending request: {webRequest.error} Response: {webRequest.downloadHandler.text}");
            }
            else
            {
                string responseText = webRequest.downloadHandler.text;
                //Debug.Log("Raw response from OpenAI: " + responseText);
                ProcessOpenAIResponse(responseText);
            }
        }
    }

    private void ProcessOpenAIResponse(string responseJson)
    {
        OpenAIResponse response = JsonUtility.FromJson<OpenAIResponse>(responseJson);

        // Check if response is not null
        if (response != null && response.choices != null && response.choices.Length > 0)
        {
            // Assuming 'content' is a string and has the description text.
            string description = response.choices[0].message.content;
            SpeakDescription(description);
        }
        else
        {
            UnityEngine.Debug.LogError("Failed to parse the OpenAI response or the response structure is not as expected: " + responseJson);
        }
    }


    private void SpeakDescription(string text)
    {
        UnityEngine.Debug.Log("Description to speak: " + text);
        // Call this method to instruct Windows to speak the given text
        InvokeTextToSpeech(text);
    }
    private void InvokeTextToSpeech(string text)
    {
        // Create a new process to invoke the built-in Windows TTS
        Process process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-Command \"Add-Type –AssemblyName System.Speech; (New-Object System.Speech.Synthesis.SpeechSynthesizer).Speak('{text}');\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();
            process.WaitForExit(); // Optionally, you can wait for the command to complete
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to invoke speech. Reason: {e.Message}");
        }
    }

    // This class structure defines how we expect the JSON from OpenAI's API to be formatted.
    [Serializable]
    private class OpenAIResponse
    {
        public Choice[] choices;

        [Serializable]
        public class Choice
        {
            public Message message; // Directly reflect the `message` structure
                                    // Remove the `finish_details` and `index`, unless you're going to use them
        }

        [Serializable]
        public class Message
        {
            public string content; // content is directly a string, not an array or an object
            public string role; // You can keep this if you need the assistant's role
                                // Any other fields you expect from the message...
        }
    }
}