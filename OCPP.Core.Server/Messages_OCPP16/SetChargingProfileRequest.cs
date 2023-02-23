using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCPP.Core.Server.Messages_OCPP16
{
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public partial class SetChargingProfileRequest
    {
        [Newtonsoft.Json.JsonProperty("connectorId", Required = Newtonsoft.Json.Required.Always)]
        public int ConnectorId { get; set; }

        [Newtonsoft.Json.JsonProperty("csChargingProfiles", Required = Newtonsoft.Json.Required.Always)]
        public ChargingProfile ChargingProfile { get; set; }


    }

    public partial class ChargingProfile
    {
        [Newtonsoft.Json.JsonProperty("chargingProfileId", Required = Newtonsoft.Json.Required.Always)]
        public int ChargingProfileId { get; set; }

        [Newtonsoft.Json.JsonProperty("stackLevel", Required = Newtonsoft.Json.Required.Always)]
        public int StackLevel { get; set; }

        [Newtonsoft.Json.JsonProperty("chargingProfilePurpose", Required = Newtonsoft.Json.Required.Always)]
        public chargingProfilePurposeType ChargingProfilePurpose { get; set; }

        [Newtonsoft.Json.JsonProperty("chargingProfileKind", Required = Newtonsoft.Json.Required.Always)]
        public chargingProfileKindType ChargingProfileKindType { get; set; }

        [Newtonsoft.Json.JsonProperty("chargingSchedule", Required = Newtonsoft.Json.Required.Always)]
        public ChargingSchedule ChargingSchedule { get; set; }


    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public enum chargingProfilePurposeType
    {
        [System.Runtime.Serialization.EnumMember(Value = @"ChargePointMaxProfile")]
        ChargePointMaxProfile = 0,

        [System.Runtime.Serialization.EnumMember(Value = @"TxDefaultProfile")]
        TxDefaultProfile = 1,

        [System.Runtime.Serialization.EnumMember(Value = @"TxProfile")]
        TxProfile = 2,
    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public enum chargingProfileKindType
    {
        [System.Runtime.Serialization.EnumMember(Value = @"Absolute")]
        Absolute = 0,

        [System.Runtime.Serialization.EnumMember(Value = @"Recurring")]
        Recurring = 1,

        [System.Runtime.Serialization.EnumMember(Value = @"Relative")]
        Relative = 2,
    }

    public partial class ChargingSchedule
    {
        [Newtonsoft.Json.JsonProperty("duration", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public int Duration { get; set; }

        [Newtonsoft.Json.JsonProperty("minChargingRate", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public double MinChargingRate { get; set; }

        [Newtonsoft.Json.JsonProperty("chargingRateUnit", Required = Newtonsoft.Json.Required.Always)]
        public ChargingRateUnitType ChargingRateUnit { get; set; }


        [Newtonsoft.Json.JsonProperty("chargingSchedulePeriod", Required = Newtonsoft.Json.Required.Always)]
        public List<ChargingSchedulePeriod> ChargingSchedulePeriod { get; set; }


    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.1.0 (Newtonsoft.Json v9.0.0.0)")]
    public enum ChargingRateUnitType
    {
        [System.Runtime.Serialization.EnumMember(Value = @"W")]
        W = 0,

        [System.Runtime.Serialization.EnumMember(Value = @"A")]
        A = 1
    }

    public partial class ChargingSchedulePeriod
    {
        [Newtonsoft.Json.JsonProperty("startPeriod", Required = Newtonsoft.Json.Required.Always)]
        public int startPeriod { get; set; }

        [Newtonsoft.Json.JsonProperty("limit", Required = Newtonsoft.Json.Required.Always)]
        public double limit { get; set; }

        public static implicit operator List<object>(ChargingSchedulePeriod v)
        {
            throw new NotImplementedException();
        }
    }
}
