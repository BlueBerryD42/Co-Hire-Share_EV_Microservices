using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;

namespace CoOwnershipVehicle.WCF.Contracts
{ 

    [ServiceContract] //Defines the service interface
    public interface IVehicleManagementService
    {
        [OperationContract] //Marks each method as a service operation
        Task<string> GetServiceInfoAsync();

        [OperationContract]
        Task<List<VehicleDTO>> GetAvailableVehiclesAsync();

        // Note: Simplified interface for demo purposes
    }

    [DataContract] //[DataContract] and [DataMember] define how objects are serialized across the network. This ensures type safety and version compatibility
    public class VehicleDTO
    {
        [DataMember]
        public int Id { get; set; }

        [DataMember]
        public string LicensePlate { get; set; }

        [DataMember]
        public string Brand { get; set; }

        [DataMember]
        public string Model { get; set; }

        [DataMember]
        public int Year { get; set; }

        [DataMember]
        public decimal PricePerDay { get; set; }

        [DataMember]
        public string Status { get; set; }
    }
}
