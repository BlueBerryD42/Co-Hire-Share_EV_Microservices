using System.Threading.Tasks;
using MassTransit;
using CoOwnershipVehicle.Shared.Contracts.Events;
using CoOwnershipVehicle.Vehicle.Api.Data;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Vehicle.Api.Consumers
{
    public class GroupCreatedConsumer : IConsumer<GroupCreatedEvent>
    {
        private readonly VehicleDbContext _context;
        private readonly ILogger<GroupCreatedConsumer> _logger;

        public GroupCreatedConsumer(VehicleDbContext context, ILogger<GroupCreatedConsumer> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<GroupCreatedEvent> context)
        {
            _logger.LogInformation("Received GroupCreatedEvent for GroupId: {GroupId}", context.Message.GroupId);

            var newGroup = new OwnershipGroup
            {
                Id = context.Message.GroupId,
                Name = context.Message.GroupName,
                Status = GroupStatus.Active, // Assuming default status is Active
                // Other properties can be set if needed
            };

            _context.OwnershipGroups.Add(newGroup);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully created OwnershipGroup with Id: {GroupId}", newGroup.Id);
        }
    }
}
