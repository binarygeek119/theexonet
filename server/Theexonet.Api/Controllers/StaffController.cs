using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Theexonet.Core.Configuration;
using Theexonet.Core.Dtos;
using Theexonet.Infrastructure.Services;

namespace Theexonet.Api.Controllers;

[ApiController]
[Route("api/staff")]
[Authorize(Policy = "Moderator")]
public class StaffController(
    StaffMessageService staffMessageService,
    PlayerMessageService playerMessageService,
    PlayerToStaffMessageService playerToStaffMessageService,
    IOptions<AdminOptions> adminOptions,
    IOptions<ModeratorOptions> moderatorOptions) : ControllerBase
{
    [HttpGet("members")]
    public ActionResult<StaffMembersResponse> Members()
    {
        var username = User.GetUsername() ?? string.Empty;
        var members = staffMessageService.GetStaffMembers(username, GetConfiguredStaff());
        return Ok(new StaffMembersResponse(members));
    }

    [HttpGet("messages")]
    public async Task<ActionResult<StaffMessagesResponse>> Messages(CancellationToken ct)
    {
        var username = User.GetUsername() ?? string.Empty;
        return Ok(new StaffMessagesResponse(await staffMessageService.GetInboxAsync(username, ct)));
    }

    [HttpGet("messages/unread-count")]
    public async Task<ActionResult<StaffUnreadCountResponse>> UnreadCount(CancellationToken ct)
    {
        var username = User.GetUsername() ?? string.Empty;
        var staffCount = await staffMessageService.GetUnreadCountAsync(username, ct);
        var playerCount = await playerToStaffMessageService.GetUnreadCountForStaffAsync(username, ct);
        return Ok(new StaffUnreadCountResponse(staffCount, playerCount));
    }

    [HttpPost("messages")]
    public async Task<ActionResult<SendStaffMessageResponse>> SendMessage(
        SendStaffMessageRequest request,
        CancellationToken ct)
    {
        var username = User.GetUsername() ?? string.Empty;
        var (message, error) = await staffMessageService.SendMessageAsync(
            username,
            request.ToUsername,
            request.Body,
            GetAllowedStaffUsernames(),
            ct);

        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(new SendStaffMessageResponse(message!, "Message sent."));
    }

    [HttpPost("messages/{messageId:guid}/read")]
    public async Task<ActionResult<StaffMessageDto>> MarkRead(Guid messageId, CancellationToken ct)
    {
        var username = User.GetUsername() ?? string.Empty;
        var (message, error) = await staffMessageService.MarkReadAsync(messageId, username, ct);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(message);
    }

    [HttpDelete("messages/{messageId:guid}")]
    public async Task<ActionResult<MessageResponse>> DeleteMessage(Guid messageId, CancellationToken ct)
    {
        var username = User.GetUsername() ?? string.Empty;
        var error = await staffMessageService.DeleteAsync(messageId, username, ct);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(new MessageResponse("Message deleted."));
    }

    [HttpGet("players/{playerId:guid}/messages")]
    public async Task<ActionResult<PlayerMessagesResponse>> PlayerMessages(Guid playerId, CancellationToken ct) =>
        Ok(new PlayerMessagesResponse(await playerMessageService.GetSentToPlayerAsync(playerId, ct)));

    [HttpPost("players/{playerId:guid}/messages")]
    public async Task<ActionResult<SendPlayerMessageResponse>> SendPlayerMessage(
        Guid playerId,
        SendPlayerMessageRequest request,
        CancellationToken ct)
    {
        var username = User.GetUsername() ?? string.Empty;
        var (message, error) = await playerMessageService.SendToPlayerAsync(username, playerId, request.Body, ct);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(new SendPlayerMessageResponse(message!, "Message sent to player."));
    }

    [HttpGet("player-inbox")]
    public async Task<ActionResult<PlayerToStaffInboxResponse>> PlayerInbox(CancellationToken ct)
    {
        var username = User.GetUsername() ?? string.Empty;
        return Ok(new PlayerToStaffInboxResponse(await playerToStaffMessageService.GetInboxForStaffAsync(username, ct)));
    }

    [HttpPost("player-inbox/{messageId:guid}/read")]
    public async Task<ActionResult<PlayerToStaffInboxDto>> MarkPlayerInboxRead(Guid messageId, CancellationToken ct)
    {
        var username = User.GetUsername() ?? string.Empty;
        var (message, error) = await playerToStaffMessageService.MarkReadAsync(messageId, username, ct);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(message);
    }

    [HttpDelete("player-inbox/{messageId:guid}")]
    public async Task<ActionResult<MessageResponse>> DeletePlayerInboxMessage(Guid messageId, CancellationToken ct)
    {
        var username = User.GetUsername() ?? string.Empty;
        var error = await playerToStaffMessageService.DeleteByStaffAsync(messageId, username, ct);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(new MessageResponse("Message deleted."));
    }

    private IReadOnlyList<StaffMemberDto> GetConfiguredStaff()
    {
        var members = new Dictionary<string, StaffMemberDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var username in adminOptions.Value.Usernames ?? [])
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                continue;
            }

            var name = username.Trim();
            members[name] = new StaffMemberDto(name, true, members.TryGetValue(name, out var existing) && existing.IsModerator);
        }

        foreach (var username in moderatorOptions.Value.Usernames ?? [])
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                continue;
            }

            var name = username.Trim();
            if (members.TryGetValue(name, out var existing))
            {
                members[name] = existing with { IsModerator = true };
            }
            else
            {
                members[name] = new StaffMemberDto(name, false, true);
            }
        }

        return members.Values.OrderBy(member => member.Username, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private HashSet<string> GetAllowedStaffUsernames() =>
        GetConfiguredStaff()
            .Select(member => member.Username)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
