using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using LightlessSync.API.Data;
using LightlessSync.API.Dto.User;
using LightlessSync.LightlessConfiguration.Models;
using LightlessSync.PlayerData.Pairs;
using LightlessSync.Services.Mediator;
using LightlessSync.UI.Handlers;
using Microsoft.Extensions.Logging;
using System.Numerics;
using System.Collections.Generic;

namespace LightlessSync.UI.Components;

public class DrawPairRequests : IMediatorSubscriber
{
    private readonly LightlessMediator _mediator;
    private readonly UiSharedService _uiSharedService;
    private readonly TagHandler _tagHandler;
    private readonly ILogger _logger;
    private readonly PairManager _pairManager;
    private bool _wasHovered = false;

    public LightlessMediator Mediator => _mediator;

    public DrawPairRequests(LightlessMediator mediator, UiSharedService uiSharedService,
        TagHandler tagHandler, ILogger logger, PairManager pairManager)
    {
        _mediator = mediator;
        _uiSharedService = uiSharedService;
        _tagHandler = tagHandler;
        _logger = logger;
        _pairManager = pairManager;
    }

    public void Dispose()
    {
    }

    public bool HasPendingRequests => _pairManager.PendingPairRequests.Any();

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
            ImGui.Text($"Pair Requests ({_pairManager.PendingPairRequests.Count})");

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
        for (int i = _pairManager.PendingPairRequests.Count - 1; i >= 0; i--)
        {
            var request = _pairManager.PendingPairRequests[i];
            DrawSinglePairRequest(request, i);
        }
    }

    private void DrawSinglePairRequest(PendingPairRequestDto request, int index)
    {
        using var id = ImRaii.PushId($"request_{index}");
        var color = ImRaii.PushColor(ImGuiCol.ChildBg, ImGui.GetColorU32(ImGuiCol.FrameBgHovered), false);

        // Calculate time remaining (30 seconds from received time)
        var remainingTime = TimeSpan.FromSeconds(30) - (DateTime.UtcNow - request.RequestTime);
        if (remainingTime.TotalSeconds <= 0)
        {
            _pairManager.PendingPairRequests.RemoveAt(index);
            return;
        }
        var progress = Math.Max(0f, Math.Min(1f, (float)(remainingTime.TotalSeconds / 30f)));

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
        var colorProgress = 1.0f - progress;
        var progressColor = Vector4.Lerp(UIColors.Get("LightlessYellow"), UIColors.Get("DimRed"), colorProgress);
        var progressFillEnd = new Vector2(progressStart.X + (progressBarWidth * progress), progressEnd.Y);
        drawList.AddRectFilled(progressStart, progressFillEnd, ImGui.ColorConvertFloat4ToU32(progressColor));

        // Add spacing for the progress bar
        ImGui.Dummy(new Vector2(0, progressBarHeight + 2));

        color.Dispose();

        // Auto-remove expired requests
    }

    private float GetRequiredHeight()
    {
        if (!_tagHandler.IsTagOpen("PairRequests"))
            return ImGui.GetFrameHeight();
        var itemHeight = ImGui.GetFrameHeightWithSpacing() + 8f;
        return ImGui.GetFrameHeight() + (_pairManager.PendingPairRequests.Count * itemHeight) + 10f;
    }


    private void HandlePairRequestResponse(PendingPairRequestDto request, bool accepted)
    {
        var userDto = new LightlessSync.API.Dto.User.UserDto(new UserData(request.RequesterUserId));
        if (accepted)
        {
            Mediator.Publish(new AcceptPairRequestMessage(userDto));
        }
        else
        {
            Mediator.Publish(new DenyPairRequestMessage(userDto));
        }

        _pairManager.PendingPairRequests.Remove(request);

        var action = accepted ? "accepted" : "denied";
        _logger.LogInformation("Pair request from {CharacterName} {Action}", request.RequesterCharacterName, action);

        _mediator.Publish(new NotificationMessage(
            $"Pair Request {(accepted ? "Accepted" : "Denied")}",
            $"You {action} the pair request from {request.RequesterCharacterName}",
            accepted ? NotificationType.Info : NotificationType.Warning,
            TimeSpan.FromSeconds(3)));
    }
}