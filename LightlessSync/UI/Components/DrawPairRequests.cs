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

        // Subscribe to incoming pair request messages
        _mediator.Subscribe<PairRequestReceivedMessage>(this, HandleIncomingPairRequest);
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
            // Draw folder header
            var icon = _tagHandler.IsTagOpen("PairRequests") ? FontAwesomeIcon.CaretDown : FontAwesomeIcon.CaretRight;
            
            ImGui.AlignTextToFramePadding();
            _uiSharedService.IconText(icon);
            
            if (ImGui.IsItemClicked())
            {
                var isOpen = _tagHandler.IsTagOpen("PairRequests");
                _tagHandler.SetTagOpen("PairRequests", !isOpen);
            }

            ImGui.SameLine();
            ImGui.Text($"Pair Requests ({_pendingRequests.Count})");

            _wasHovered = ImGui.IsItemHovered();

            // Draw requests if folder is open
            if (_tagHandler.IsTagOpen("PairRequests"))
            {
                ImGui.Indent();
                DrawPairRequestsList();
                ImGui.Unindent();
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
        
        // Request info
        ImGui.AlignTextToFramePadding();
        ImGui.Text($"From: {request.RequesterCharacterName}");
        
        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 120); // Position buttons on the right

        // Allow button
        if (_uiSharedService.IconTextButton(FontAwesomeIcon.Check, "Allow", 50))
        {
            HandlePairRequestResponse(request, true);
            _pendingRequests.RemoveAt(index);
        }

        ImGui.SameLine();

        // Deny button  
        if (_uiSharedService.IconTextButton(FontAwesomeIcon.Times, "Deny", 50))
        {
            HandlePairRequestResponse(request, false);
            _pendingRequests.RemoveAt(index);
        }
    }

    private float GetRequiredHeight()
    {
        if (!_tagHandler.IsTagOpen("PairRequests"))
            return ImGui.GetFrameHeight();

        return ImGui.GetFrameHeight() + (_pendingRequests.Count * ImGui.GetFrameHeightWithSpacing());
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
}

public class PairRequestDto
{
    public UserData RequesterUser { get; set; } = null!;
    public string RequesterCharacterName { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
}
