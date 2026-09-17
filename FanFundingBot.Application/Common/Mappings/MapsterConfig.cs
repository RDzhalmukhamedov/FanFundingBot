using System.Diagnostics.CodeAnalysis;
using FanFundingBot.Application.Orders.DTOs;
using FanFundingBot.Application.Projects.DTOs;
using FanFundingBot.Application.Questions.DTOs;
using FanFundingBot.Domain.Entities;
using Mapster;

namespace FanFundingBot.Application.Common.Mappings;

[ExcludeFromCodeCoverage]
public static class MapsterConfig
{
    public static void Register()
    {
        TypeAdapterConfig<Order, OrderDto>.NewConfig()
            .Map(dest => dest.UserId, src => src.User.Id)
            .Map(dest => dest.Username, src => src.User.Username)
            .Map(dest => dest.FirstName, src => src.User.FirstName)
            .Map(dest => dest.ProjectId, src => src.Project.Id)
            .Map(dest => dest.ProjectName, src => src.Project.Name)
            .Map(dest => dest.Phone, src => src.Phone.ToString())
            .Map(dest => dest.Address, src => src.Address.ToString())
            .Map(dest => dest.Positions, src => src.OrderAddons
                .Select(p => new OrderPositionDto(
                    p.Addon.Id,
                    p.Addon.Name,
                    p.Addon.Price,
                    p.Quantity)))
            .Map(dest => dest.Total, src => (decimal)src.Total);

        TypeAdapterConfig<OrderPosition, OrderPositionDto>.NewConfig()
            .Map(dest => dest.AddonId, src => src.Addon.Id)
            .Map(dest => dest.AddonName, src => src.Addon.Name)
            .Map(dest => dest.Price, src => (decimal)src.Addon.Price)
            .Map(dest => dest.Quantity, src => (int)src.Quantity);

        TypeAdapterConfig<Project, ProjectDto>.NewConfig()
            .Map(dest => dest.StatusId, src => src.Status.Id)
            .Map(dest => dest.StatusName, src => src.Status.DisplayName)
            .Map(dest => dest.CanEditOrders, src => src.Status.CanEditOrders)
            .Map(dest => dest.Addons, src => src.Addons.Adapt<List<AddonDto>>())
            .Map(dest => dest.Members, src => src.Members.Adapt<List<ProjectMemberDto>>());

        TypeAdapterConfig<Addon, AddonDto>.NewConfig()
            .Map(dest => dest.Price, src => (decimal)src.Price);

        TypeAdapterConfig<ProjectMember, ProjectMemberDto>.NewConfig();

        TypeAdapterConfig<ProjectStatus, ProjectStatusDto>.NewConfig();

        TypeAdapterConfig<Question, QuestionDto>.NewConfig()
            .Map(dest => dest.UserId, src => src.User.Id)
            .Map(dest => dest.Username, src => src.User.Username)
            .Map(dest => dest.FirstName, src => src.User.FirstName);
    }
}
