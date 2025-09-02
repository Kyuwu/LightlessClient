using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using LightlessSync.API.Data;
using LightlessSync.LightlessConfiguration.Models;
using LightlessSync.Services.Mediator;
using LightlessSync.UI.Handlers;
using Microsoft.Extensions.Logging;
using System.Numerics;

namespace LightlessSync.UI.Components;

public class DrawPairRequests : IMediatorSubscriber
{
    private readonly LightlessMediator _mediator;
    private readonly UiSharedService _uiSharedService;
    private readonly TagHandler _tagHandler;
    private readonly ILogger _logger;
    private readonly List<PairRequestDto> _pendingRequests;
    private bool _wasHovered = false;

    public LightlessMediator Mediator => _mediator;

    public DrawPairRequests(LightlessMediator mediator, UiSharedService uiSharedService, 
        TagHandler tagHandler, ILogger logger)
    {
        _mediator = mediator;
        _uiSharedService = uiSharedService;
        _tagHandler = tagHandler;
        _logger = logger;
        _pendingRequests = new List<PairRequestDto>();
        _mediator.Subscribe<PairRequestReceivedMessage>(this, HandleIncomingPairRequest);

        // Add dummy data for demo
        AddDummyData();
    }

    public void Dispose()
    {
        _mediator.UnsubscribeAll(this);
    }

    public bool HasPendingRequests => _pendingRequests.Any();

    public void Draw()
    {
        if (!HasPendingRequests) return;

        using var id = ImRaii.PushId("pair_requests_folder");
        var color = ImRaii.PushColor(ImGuiCol.ChildBg, ImGui.GetColorU32(ImGuiCol.FrameBgHovered), _wasHovered);

        using (ImRaii.Child("pair_requests_folder", new Vector2(UiSharedService.GetWindowContentRegionWidth() - ImGui.GetCursorPosX(), GetRequiredHeight())))
        {
            var dropdownIcon = _tagHandler.IsTagOpen("PairRequests") ? FontAwesomeIcon.CaretDown : FontAwesomeIcon.CaretRight;

            ImGui.AlignTextToFramePadding();

            _uiSharedService.IconText(dropdownIcon);
            if (ImGui.IsItemClicked())
            {
                _tagHandler.SetTagOpen("PairRequests", !_tagHandler.IsTagOpen("PairRequests"));
            }

            ImGui.SameLine();

            using (ImRaii.PushColor(ImGuiCol.Text, UIColors.Get("LightlessBlue")))
            {
                _uiSharedService.IconText(FontAwesomeIcon.UserPlus);
            }
            var leftSideEnd = ImGui.GetCursorPosX();

            ImGui.SameLine();
            ImGui.Text($"Pair Requests ({_pendingRequests.Count})");

            _wasHovered = ImGui.IsItemHovered();
            color.Dispose();

            ImGui.Separator();

            if (_tagHandler.IsTagOpen("PairRequests"))
            {
                using var indent = ImRaii.PushIndent(_uiSharedService.GetIconSize(FontAwesomeIcon.EllipsisV).X + ImGui.GetStyle().ItemSpacing.X, false);
                DrawPairRequestsList();
                ImGui.Separator();
            }
        }
    }

    private void DrawPairRequestsList()
    {
        for (int i = _pendingRequests.Count - 1; i >= 0; i--)
        {
            var request = _pendingRequests[i];
            DrawSinglePairRequest(request, i);
        }
    }

    private void DrawSinglePairRequest(PairRequestDto request, int index)
    {
        using var id = ImRaii.PushId($"request_{index}");
        var color = ImRaii.PushColor(ImGuiCol.ChildBg, ImGui.GetColorU32(ImGuiCol.FrameBgHovered), false);
        
        // Calculate time remaining (30 seconds from received time)
        var timeElapsed = DateTime.UtcNow - request.ReceivedAt;
        var totalTime = TimeSpan.FromSeconds(30);
        var timeRemaining = totalTime - timeElapsed;
        var progress = Math.Max(0f, Math.Min(1f, (float)(timeElapsed.TotalSeconds / totalTime.TotalSeconds)));
        
        using (ImRaii.Child($"request_{index}", new Vector2(UiSharedService.GetWindowContentRegionWidth() - ImGui.GetCursorPosX(), ImGui.GetFrameHeight())))
        {
            // Left side - icon
            _uiSharedService.IconText(FontAwesomeIcon.UserPlus, UIColors.Get("LightlessBlue"));
            ImGui.SameLine();
            var posX = ImGui.GetCursorPosX();
            
            // Right side - buttons (calculate from right to left like DrawUserPair)
            var spacingX = ImGui.GetStyle().ItemSpacing.X;
            var denyButtonSize = _uiSharedService.GetIconButtonSize(FontAwesomeIcon.Times);
            var allowButtonSize = _uiSharedService.GetIconButtonSize(FontAwesomeIcon.Check);
            var windowEndX = ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth();
            float currentRightSide = windowEndX - denyButtonSize.X;
            
            // Deny button (rightmost)
            ImGui.SameLine(currentRightSide);
            ImGui.PushStyleColor(ImGuiCol.Button, ImGui.ColorConvertFloat4ToU32(UIColors.Get("DimRed")));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ImGui.ColorConvertFloat4ToU32(UIColors.Get("DimRed") * 1.1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, ImGui.ColorConvertFloat4ToU32(UIColors.Get("DimRed") * 0.9f));
            
            if (_uiSharedService.IconButton(FontAwesomeIcon.Times))
            {
                HandlePairRequestResponse(request, false);
                _pendingRequests.RemoveAt(index);
            }
            ImGui.PopStyleColor(3);
            
            // Allow button (second from right)
            currentRightSide -= (allowButtonSize.X + spacingX);
            ImGui.SameLine(currentRightSide);
            ImGui.PushStyleColor(ImGuiCol.Button, ImGui.ColorConvertFloat4ToU32(UIColors.Get("LightlessPurple")));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ImGui.ColorConvertFloat4ToU32(UIColors.Get("LightlessPurple") * 1.1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, ImGui.ColorConvertFloat4ToU32(UIColors.Get("LightlessPurple") * 0.9f));
            
            if (_uiSharedService.IconButton(FontAwesomeIcon.Check))
            {
                HandlePairRequestResponse(request, true);
                _pendingRequests.RemoveAt(index);
            }
            ImGui.PopStyleColor(3);
            
            // Name in the middle
            ImGui.SameLine(posX);
            ImGui.AlignTextToFramePadding();
            ImGui.Text(request.RequesterCharacterName);
            
        }
        
        // Progress bar below the child window
        var progressBarHeight = 2f;
        var progressBarWidth = UiSharedService.GetWindowContentRegionWidth() - ImGui.GetCursorPosX();
        var drawList = ImGui.GetWindowDrawList();
        var cursorScreenPos = ImGui.GetCursorScreenPos();
        var progressStart = new Vector2(cursorScreenPos.X, cursorScreenPos.Y);
        var progressEnd = new Vector2(progressStart.X + progressBarWidth, progressStart.Y + progressBarHeight);
        
        // Background
        drawList.AddRectFilled(progressStart, progressEnd, ImGui.GetColorU32(ImGuiCol.FrameBg));
        
        // Progress fill
        var progressColor = Vector4.Lerp(UIColors.Get("LightlessYellow"), UIColors.Get("DimRed"), progress);
        var progressFillEnd = new Vector2(progressStart.X + (progressBarWidth * progress), progressEnd.Y);
        drawList.AddRectFilled(progressStart, progressFillEnd, ImGui.ColorConvertFloat4ToU32(progressColor));
        
        // Add spacing for the progress bar
        ImGui.Dummy(new Vector2(0, progressBarHeight + 2));
        
        color.Dispose();
        
        // Auto-remove expired requests
        if (timeRemaining <= TimeSpan.Zero)
        {
            _pendingRequests.RemoveAt(index);
            _logger.LogInformation("Pair request from {CharacterName} expired", request.RequesterCharacterName);
            
            // Notify user of expired pair request
            _mediator.Publish(new NotificationMessage(
                "Pair Request Expired",
                $"The pair request from {request.RequesterCharacterName} has expired",
                NotificationType.Warning,
                TimeSpan.FromSeconds(3)));
        }
    }

    private float GetRequiredHeight()
    {
        if (!_tagHandler.IsTagOpen("PairRequests"))
            return ImGui.GetFrameHeight();
        var itemHeight = ImGui.GetFrameHeightWithSpacing() + 8f;
        return ImGui.GetFrameHeight() + (_pendingRequests.Count * itemHeight) + 10f;
    }

    private void HandleIncomingPairRequest(PairRequestReceivedMessage message)
    {
        var request = new PairRequestDto
        {
            RequesterUser = message.RequesterUser,
            RequesterCharacterName = message.RequesterCharacterName,
            ReceivedAt = DateTime.UtcNow
        };

        _pendingRequests.Add(request);
        _logger.LogInformation("Received pair request from {CharacterName}", message.RequesterCharacterName);
        
        // Notify user of new pair request
        _mediator.Publish(new NotificationMessage(
            "New Pair Request",
            $"{message.RequesterCharacterName} wants to pair with you - Open Lightless UI to respond",
            NotificationType.Info,
            TimeSpan.FromSeconds(5)));
    }

    private void HandlePairRequestResponse(PairRequestDto request, bool accepted)
    {
        _mediator.Publish(new PairRequestResponseMessage(request.RequesterUser, accepted));
        
        var action = accepted ? "accepted" : "denied";
        _logger.LogInformation("Pair request from {CharacterName} {Action}", request.RequesterCharacterName, action);
        
        _mediator.Publish(new NotificationMessage(
            $"Pair Request {(accepted ? "Accepted" : "Denied")}", 
            $"You {action} the pair request from {request.RequesterCharacterName}",
            accepted ? NotificationType.Info : NotificationType.Warning,
            TimeSpan.FromSeconds(3)));
    }

    private void AddDummyData()
    {
        // Simulate incoming pair requests for demo (this will trigger notifications)
        var dummyRequests = new[]
        {
            new { User = new UserData("demo-uid-1", "Chocola Himari"), Name = "Chocola Himari", SecondsAgo = -15 },
            new { User = new UserData("demo-uid-2", "Hikari Moriko"), Name = "Hikari Moriko", SecondsAgo = -2 },
            new { User = new UserData("demo-uid-3", "Panda deeznuts"), Name = "Panda deeznuts", SecondsAgo = 0 }
        };

        foreach (var dummy in dummyRequests)
        {
            // Create the request manually to set custom timestamp
            var request = new PairRequestDto
            {
                RequesterUser = dummy.User,
                RequesterCharacterName = dummy.Name,
                ReceivedAt = DateTime.UtcNow.AddSeconds(dummy.SecondsAgo)
            };
            
            _pendingRequests.Add(request);
            
            // Trigger notification for demo
            _mediator.Publish(new NotificationMessage(
                "New Pair Request",
                $"{dummy.Name} wants to pair with you - Open Lightless UI to respond",
                NotificationType.Info,
                TimeSpan.FromSeconds(5)));
        }

        _logger.LogInformation("[DEMO] Added {Count} dummy pair requests with notifications", _pendingRequests.Count);
    }
}

public class PairRequestDto
{
    public UserData RequesterUser { get; set; } = null!;
    public string RequesterCharacterName { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
}
