using System;
using UnityEngine;

/// <summary>
/// Visual styles available for the highlight border (hover/selection) on Gallery thumbnails.
/// </summary>
[Serializable]
public enum GalleryBorderStyle
{
    /// <summary>Continuous solid border drawn inward along thumbnail edges.</summary>
    SolidInside = 0,

    /// <summary>Continuous solid border projected outward along thumbnail edges.</summary>
    SolidOutside = 1,

    /// <summary>Corner brackets resembling a camera viewfinder.</summary>
    CornerBrackets = 2
}

/// <summary>
/// Event payload published on EventBus ("Gallery_PhotoSent") when the player sends a photo to a contact.
/// </summary>
[Serializable]
public struct GalleryPhotoSentPayload
{
    public string PhotoId;
    public string ImageKey;
    public string ContactName;
}

/// <summary>
/// Serializable data model representing a photo stored in the Gallery application.
/// Supports direct Sprites assigned from Inspector as well as paths compatible with StreamingAssets / Resources.
/// </summary>
[Serializable]
public class GalleryPhotoData
{
    [Tooltip("Optional unique photo identifier. If left empty, it is generated from the image path or Sprite name.")]
    [SerializeField]
    private string _id;

    [Tooltip("Optional title or descriptive label shown in full-size preview.")]
    [SerializeField]
    private string _title;

    [Tooltip("Optional caption attached to the image message when sent via chat (empty = image only).")]
    [SerializeField]
    private string _defaultCaption;

    [Tooltip("Direct Sprite reference (optional if imagePath is provided in StreamingAssets/Resources).")]
    [SerializeField]
    private Sprite _sprite;

    [Tooltip("Image path relative to StreamingAssets or Resources (e.g. 'Images/Candy.png').")]
    [SerializeField]
    private string _imagePath;

    [Tooltip("Specifies whether the photo is unlocked and visible in the Gallery from the start.")]
    [SerializeField]
    private bool _unlocked = true;

    [Tooltip("Optional: Name of the event to publish via EventBus when the player shares this photo.")]
    [SerializeField]
    private string _eventOnShare;

    public GalleryPhotoData() { }

    public GalleryPhotoData(string id, string title, string imagePath, Sprite sprite = null, bool unlocked = true, string defaultCaption = "")
    {
        _id = id;
        _title = title;
        _imagePath = imagePath;
        _sprite = sprite;
        _unlocked = unlocked;
        _defaultCaption = defaultCaption;
    }

    public string Id
    {
        get
        {
            if (!string.IsNullOrEmpty(_id)) return _id;
            if (!string.IsNullOrEmpty(_imagePath)) return System.IO.Path.GetFileNameWithoutExtension(_imagePath);
            if (_sprite != null) return _sprite.name;
            return "photo_unnamed";
        }
        set => _id = value;
    }

    public string Title
    {
        get
        {
            if (!string.IsNullOrEmpty(_title)) return _title;
            if (!string.IsNullOrEmpty(_imagePath)) return System.IO.Path.GetFileNameWithoutExtension(_imagePath);
            if (_sprite != null) return _sprite.name;
            return string.Empty;
        }
        set => _title = value;
    }

    public string DefaultCaption
    {
        get => _defaultCaption ?? string.Empty;
        set => _defaultCaption = value;
    }

    public Sprite DirectSprite
    {
        get => _sprite;
        set => _sprite = value;
    }

    public string ImagePath
    {
        get => _imagePath;
        set => _imagePath = value;
    }

    public bool IsUnlocked
    {
        get => _unlocked;
        set => _unlocked = value;
    }

    public string EventOnShare
    {
        get => _eventOnShare;
        set => _eventOnShare = value;
    }

    /// <summary>
    /// Returns the effective key/path used by the Messages system (_imagePath in Message)
    /// to retrieve the Sprite from MessagesUI cache or load from disk.
    /// </summary>
    public string GetEffectiveImageKey()
    {
        if (!string.IsNullOrEmpty(_imagePath))
        {
            return _imagePath.Replace('\\', '/');
        }

        if (_sprite != null)
        {
            return $"gallery://{Id}";
        }

        return string.Empty;
    }

    /// <summary>
    /// Resolves and returns the Sprite associated with this photo, caching it in MessagesUI
    /// to guarantee full compatibility when sent as an image message in both Editor and Standalone builds.
    /// </summary>
    public Sprite ResolveSprite()
    {
        string key = GetEffectiveImageKey();

        if (_sprite == null && !string.IsNullOrEmpty(_imagePath))
        {
            _sprite = MessagesUI.LoadSprite(_imagePath);
        }

        if (_sprite != null && !string.IsNullOrEmpty(key))
        {
            MessagesUI.RegisterSpriteInCache(key, _sprite);
        }

        return _sprite;
    }
}
