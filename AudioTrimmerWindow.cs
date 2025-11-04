// Copyright (c) 2025 Ahmed Shahin
// This script is part of the Unity Scripts collection.
// Licensed under the MIT License. See LICENSE file in the project root for details.


using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

public class AudioTrimmerWindow : EditorWindow
{
    private AudioClip selectedClip;
    private AudioClip previewClip;
    private float startTime = 0f;
    private float endTime = 0f;
    private float volume = 1f;
    private float pitch = 1f;
    private bool isPlaying = false;
    private AudioSource previewSource;
    private Vector2 scrollPos;
    private Texture2D waveformTexture;
    private bool waveformNeedsUpdate = true;
    
    [MenuItem("Window/Audio Trimmer")]
    public static void ShowWindow()
    {
        GetWindow<AudioTrimmerWindow>("Audio Trimmer");
    }

    private void OnEnable()
    {
        CreatePreviewSource();
    }

    private void OnDisable()
    {
        StopPreview();
        if (previewSource != null)
        {
            DestroyImmediate(previewSource.gameObject);
        }
    }

    private void CreatePreviewSource()
    {
        GameObject go = new GameObject("AudioPreviewSource");
        go.hideFlags = HideFlags.HideAndDontSave;
        previewSource = go.AddComponent<AudioSource>();
        previewSource.playOnAwake = false;
    }

    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Audio Trimmer & Modifier", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        
        // Audio Clip Selection
        EditorGUI.BeginChangeCheck();
        selectedClip = (AudioClip)EditorGUILayout.ObjectField("Audio Clip", selectedClip, typeof(AudioClip), false);
        if (EditorGUI.EndChangeCheck() && selectedClip != null)
        {
            endTime = selectedClip.length;
            startTime = 0f;
            waveformNeedsUpdate = true;
            StopPreview();
        }

        if (selectedClip == null)
        {
            EditorGUILayout.HelpBox("Select an audio clip to begin editing.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        EditorGUILayout.Space(10);
        
        // Display waveform
        DrawWaveform();
        
        EditorGUILayout.Space(10);
        
        // Trim Controls
        EditorGUILayout.LabelField("Trim Settings", EditorStyles.boldLabel);
        
        float maxTime = selectedClip.length;
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Start Time:", GUILayout.Width(70));
        startTime = EditorGUILayout.Slider(startTime, 0f, endTime - 0.01f);
        EditorGUILayout.LabelField($"{startTime:F2}s", GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("End Time:", GUILayout.Width(70));
        endTime = EditorGUILayout.Slider(endTime, startTime + 0.01f, maxTime);
        EditorGUILayout.LabelField($"{endTime:F2}s", GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.LabelField($"Duration: {(endTime - startTime):F2} seconds");
        
        EditorGUILayout.Space(10);
        
        // Modification Controls
        EditorGUILayout.LabelField("Audio Modifications", EditorStyles.boldLabel);
        
        volume = EditorGUILayout.Slider("Volume", volume, 0f, 2f);
        pitch = EditorGUILayout.Slider("Pitch", pitch, 0.5f, 2f);
        
        EditorGUILayout.Space(10);
        
        // Preview Controls
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button(isPlaying ? "Stop Preview" : "Play Preview"))
        {
            if (isPlaying)
                StopPreview();
            else
                PlayPreview();
        }
        
        if (GUILayout.Button("Play Full Audio"))
        {
            PlayFullAudio();
        }
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(10);
        
        // Export Controls
        EditorGUILayout.LabelField("Export", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Save Trimmed Audio", GUILayout.Height(30)))
        {
            SaveTrimmedAudio();
        }
        
        EditorGUILayout.HelpBox(
            "Note: Unity's AudioClip doesn't support direct MP3 encoding. " +
            "The trimmed audio will be saved as WAV format. " +
            "Volume and pitch modifications are applied during export.",
            MessageType.Info
        );
        
        EditorGUILayout.EndScrollView();
    }

    private void DrawWaveform()
    {
        if (selectedClip == null) return;
        
        if (waveformNeedsUpdate || waveformTexture == null)
        {
            GenerateWaveform();
            waveformNeedsUpdate = false;
        }
        
        if (waveformTexture != null)
        {
            Rect waveformRect = GUILayoutUtility.GetRect(position.width - 20, 100);
            EditorGUI.DrawPreviewTexture(waveformRect, waveformTexture);
            
            // Draw trim markers
            float startX = waveformRect.x + (startTime / selectedClip.length) * waveformRect.width;
            float endX = waveformRect.x + (endTime / selectedClip.length) * waveformRect.width;
            
            EditorGUI.DrawRect(new Rect(startX, waveformRect.y, 2, waveformRect.height), Color.green);
            EditorGUI.DrawRect(new Rect(endX, waveformRect.y, 2, waveformRect.height), Color.red);
        }
    }

    private void GenerateWaveform()
    {
        int width = 512;
        int height = 100;
        
        waveformTexture = new Texture2D(width, height);
        Color[] pixels = new Color[width * height];
        
        // Fill with background
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color(0.2f, 0.2f, 0.2f);
        
        // Get audio data
        float[] samples = new float[selectedClip.samples * selectedClip.channels];
        selectedClip.GetData(samples, 0);
        
        // Draw waveform
        int samplesPerPixel = samples.Length / width;
        
        for (int x = 0; x < width; x++)
        {
            int sampleIndex = x * samplesPerPixel;
            float max = 0f;
            
            for (int i = 0; i < samplesPerPixel && sampleIndex + i < samples.Length; i++)
            {
                max = Mathf.Max(max, Mathf.Abs(samples[sampleIndex + i]));
            }
            
            int waveHeight = Mathf.RoundToInt(max * height * 0.8f);
            int centerY = height / 2;
            
            for (int y = centerY - waveHeight / 2; y < centerY + waveHeight / 2; y++)
            {
                if (y >= 0 && y < height)
                    pixels[y * width + x] = new Color(0.3f, 0.6f, 1f);
            }
        }
        
        waveformTexture.SetPixels(pixels);
        waveformTexture.Apply();
    }

    private void PlayPreview()
    {
        if (previewSource == null) CreatePreviewSource();
        
        previewSource.clip = selectedClip;
        previewSource.time = startTime;
        previewSource.volume = volume;
        previewSource.pitch = pitch;
        previewSource.Play();
        isPlaying = true;
        
        EditorApplication.update += CheckPreviewEnd;
    }

    private void PlayFullAudio()
    {
        if (previewSource == null) CreatePreviewSource();
        
        StopPreview();
        previewSource.clip = selectedClip;
        previewSource.time = 0f;
        previewSource.volume = 1f;
        previewSource.pitch = 1f;
        previewSource.Play();
    }

    private void StopPreview()
    {
        if (previewSource != null && previewSource.isPlaying)
        {
            previewSource.Stop();
        }
        isPlaying = false;
        EditorApplication.update -= CheckPreviewEnd;
    }

    private void CheckPreviewEnd()
    {
        if (previewSource != null && previewSource.time >= endTime)
        {
            StopPreview();
        }
    }

    private void SaveTrimmedAudio()
    {
        string path = EditorUtility.SaveFilePanel(
            "Save Trimmed Audio",
            "Assets",
            selectedClip.name + "_trimmed.wav",
            "wav"
        );
        
        if (string.IsNullOrEmpty(path)) return;
        
        // Get samples from the original clip
        float[] originalSamples = new float[selectedClip.samples * selectedClip.channels];
        selectedClip.GetData(originalSamples, 0);
        
        // Calculate trim positions
        int startSample = Mathf.RoundToInt(startTime * selectedClip.frequency) * selectedClip.channels;
        int endSample = Mathf.RoundToInt(endTime * selectedClip.frequency) * selectedClip.channels;
        int trimmedLength = endSample - startSample;
        
        // Extract trimmed samples and apply modifications
        float[] trimmedSamples = new float[trimmedLength];
        for (int i = 0; i < trimmedLength; i++)
        {
            trimmedSamples[i] = originalSamples[startSample + i] * volume;
        }
        
        // Save as WAV
        SaveWav(path, trimmedSamples, selectedClip.frequency, selectedClip.channels);
        
        EditorUtility.DisplayDialog("Success", "Audio saved successfully!", "OK");
        AssetDatabase.Refresh();
    }

    private void SaveWav(string path, float[] samples, int frequency, int channels)
    {
        using (FileStream fs = new FileStream(path, FileMode.Create))
        using (BinaryWriter writer = new BinaryWriter(fs))
        {
            int sampleCount = samples.Length;
            int byteRate = frequency * channels * 2;
            
            // WAV Header
            writer.Write(new char[4] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + sampleCount * 2);
            writer.Write(new char[4] { 'W', 'A', 'V', 'E' });
            writer.Write(new char[4] { 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(frequency);
            writer.Write(byteRate);
            writer.Write((short)(channels * 2));
            writer.Write((short)16);
            writer.Write(new char[4] { 'd', 'a', 't', 'a' });
            writer.Write(sampleCount * 2);
            
            // Audio data
            foreach (float sample in samples)
            {
                short intSample = (short)(sample * 32767f);
                writer.Write(intSample);
            }
        }
    }
}
