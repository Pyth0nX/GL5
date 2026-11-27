using System;
using UnityEngine;

public enum ConnectTab
{
    Messages = 0,
    Profile = 1
}

[Serializable]
public class ConnectProfileData
{
    public string displayName = string.Empty;
    public string handle = string.Empty;
    public bool usedRealName = false;
    public string firstName = string.Empty;
    public string schoolName = string.Empty;
    public int avatarIndex = 0;
    public string avatarImagePath = string.Empty;
    public bool allowLocationAccess = false;
    public bool isPersonalPhoto = false;
    public bool isPrivateAccount = false;
    public int resolvedPrivacyRoute = 1;
    public string bioText = string.Empty;

    public bool IsPopulated => !string.IsNullOrWhiteSpace(displayName);

    public ConnectProfileData Clone()
    {
        return new ConnectProfileData
        {
            displayName = displayName ?? string.Empty,
            handle = handle ?? string.Empty,
            usedRealName = usedRealName,
            firstName = firstName ?? string.Empty,
            schoolName = schoolName ?? string.Empty,
            avatarIndex = avatarIndex,
            avatarImagePath = avatarImagePath ?? string.Empty,
            allowLocationAccess = allowLocationAccess,
            isPersonalPhoto = isPersonalPhoto,
            isPrivateAccount = isPrivateAccount,
            resolvedPrivacyRoute = resolvedPrivacyRoute,
            bioText = bioText ?? string.Empty
        };
    }
}

[Serializable]
public class ConnectPostData
{
    public string postId = string.Empty;
    public string caption = string.Empty;
    public string imageKey = string.Empty;
    public string locationTag = string.Empty;
    public string timestampLabel = string.Empty;
    public int likesCount = 0;
    public int commentsCount = 0;
    public string topCommentPreview = string.Empty;
    public int dayPosted = 1;
    public bool isDeleted = false;

    public ConnectPostData Clone()
    {
        return new ConnectPostData
        {
            postId = postId ?? string.Empty,
            caption = caption ?? string.Empty,
            imageKey = imageKey ?? string.Empty,
            locationTag = locationTag ?? string.Empty,
            timestampLabel = timestampLabel ?? string.Empty,
            likesCount = likesCount,
            commentsCount = commentsCount,
            topCommentPreview = topCommentPreview ?? string.Empty,
            dayPosted = dayPosted,
            isDeleted = isDeleted
        };
    }
}

[Serializable]
public class ConnectPostCatalogEntry
{
    [Tooltip("Unique identifier for this post entry (prevents duplicates).")]
    public string postId;

    [Tooltip("EventBus event that publishes or mutates this post (e.g. Start_Day3, Choice4_Safe).")]
    public string triggerEventName;

    [Min(1)]
    public int dayPosted = 1;

    [TextArea(2, 4)]
    public string caption;

    [Tooltip("Optional direct Sprite reference displayed in the post card.")]
    public Sprite customImageSprite;

    [Tooltip("Optional sprite key/path resolved via MessagesUI.LoadSprite (e.g. LostCause.png, Candy.png).")]
    public string imageKey;

    public string locationTag;
    public string timestampLabel;

    [Min(0)]
    public int initialLikes = 10;

    [Min(0)]
    public int initialComments = 2;

    public string topCommentPreview;

    [Tooltip("If enabled, firing triggerEventName deletes targetPostIdToDelete instead of adding a post.")]
    public bool deletePostOnTrigger = false;

    public string targetPostIdToDelete;
}
