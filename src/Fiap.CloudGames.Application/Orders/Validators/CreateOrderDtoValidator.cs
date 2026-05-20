using Fiap.CloudGames.Application.Orders.Dtos;
using FluentValidation;

namespace Fiap.CloudGames.Application.Orders.Validators;

public class CreateOrderDtoValidator : AbstractValidator<CreateOrderDto>
{
    public CreateOrderDtoValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("O usuário é obrigatório.");

        RuleFor(x => x.Items)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Os itens do pedido são obrigatórios.")
            .NotEmpty().WithMessage("Informe ao menos um item no pedido.");

        RuleForEach(x => x.Items)
            .SetValidator(new CreateOrderItemDtoValidator());
    }
}
