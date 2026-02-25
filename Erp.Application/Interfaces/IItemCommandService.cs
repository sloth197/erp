using Erp.Application.Commands;
using Erp.Application.DTOs;

namespace Erp.Application.Interfaces;

public interface IItemCommandService
{
    Task<ItemCommandResultDto> CreateItemAsync(CreateItemCommand command, CancellationToken cancellationToken = default);
    Task<ItemCommandResultDto> UpdateItemAsync(UpdateItemCommand command, CancellationToken cancellationToken = default);
    Task<ItemCommandResultDto> ActivateItemAsync(ActivateItemCommand command, CancellationToken cancellationToken = default);
    Task<ItemCommandResultDto> DeactivateItemAsync(DeactivateItemCommand command, CancellationToken cancellationToken = default);
}
