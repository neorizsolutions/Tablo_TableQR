using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tablo.Api.DTOs;

namespace Tablo.Api.Interfaces
{
    public interface ISupportService
    {
        Task<SupportSyncResponseDto> GetRestaurantConversationsAsync(
            Guid restaurantId,
            DateTime? sinceUtc = null,
            CancellationToken cancellationToken = default);

        Task<SupportConversationDetailDto?> GetConversationDetailAsync(
            Guid conversationId,
            Guid? restaurantId,
            long? afterSequence = null,
            CancellationToken cancellationToken = default);

        Task<SupportConversationMutationResultDto> CreateConversationAsync(
            Guid restaurantId,
            Guid employeeId,
            CreateSupportConversationDto request,
            CancellationToken cancellationToken = default);

        Task<SupportConversationMutationResultDto> AddRestaurantReplyAsync(
            Guid conversationId,
            Guid restaurantId,
            Guid employeeId,
            CreateSupportMessageDto request,
            CancellationToken cancellationToken = default);

        Task<SupportConversationMutationResultDto> ReopenConversationAsync(
            Guid conversationId,
            Guid restaurantId,
            Guid employeeId,
            ReopenSupportConversationDto request,
            CancellationToken cancellationToken = default);

        Task<SupportConversationSummaryDto?> MarkRestaurantReadAsync(
            Guid conversationId,
            Guid restaurantId,
            string actorName,
            CancellationToken cancellationToken = default);

        Task<SupportConversationSummaryDto?> ResolveConversationAsync(
            Guid conversationId,
            Guid? restaurantId,
            string actorType,
            string actorName,
            string? note = null,
            CancellationToken cancellationToken = default);

        Task<SupportSyncResponseDto> GetSuperAdminInboxAsync(
            string? statusFilter = null,
            string? search = null,
            bool? escalatedOnly = null,
            DateTime? sinceUtc = null,
            CancellationToken cancellationToken = default);

        Task<SupportConversationMutationResultDto> AddSuperAdminReplyAsync(
            Guid conversationId,
            string actorName,
            string actorEmail,
            CreateSupportMessageDto request,
            CancellationToken cancellationToken = default);

        Task<SupportConversationSummaryDto?> MarkSuperAdminReadAsync(
            Guid conversationId,
            string actorName,
            CancellationToken cancellationToken = default);
    }
}
