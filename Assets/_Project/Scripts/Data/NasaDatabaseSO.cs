using System.Collections.Generic;
using System.Linq;
using MissionDesign.Core;
using UnityEngine;

namespace MissionDesign.Data
{
    /// <summary>Catálogo central que leen la UI y los selectores de cada rol.</summary>
    [CreateAssetMenu(menuName = "NASA SpaceApps/Database", fileName = "NasaDatabase")]
    public class NasaDatabaseSO : ScriptableObject
    {
        public List<LaunchVehicleSO> launchVehicles = new List<LaunchVehicleSO>();
        public List<OrbitSO> orbits = new List<OrbitSO>();
        public List<InstrumentSO> instruments = new List<InstrumentSO>();
        public List<SpacecraftBusSO> buses = new List<SpacecraftBusSO>();
        public List<SolarArraySO> solarArrays = new List<SolarArraySO>();
        public List<BatterySO> batteries = new List<BatterySO>();
        public List<AntennaSO> antennas = new List<AntennaSO>();
        public List<OnboardComputerSO> computers = new List<OnboardComputerSO>();
        public List<AttitudeControlSO> attitudeControls = new List<AttitudeControlSO>();
        public List<GroundNetworkSO> groundNetworks = new List<GroundNetworkSO>();
        public List<RandomEventSO> randomEvents = new List<RandomEventSO>();
        public List<MissionScenarioSO> scenarios = new List<MissionScenarioSO>();
        public List<RealMissionSO> realMissions = new List<RealMissionSO>();

        public OrbitSO GetOrbit(OrbitRegime regime) => orbits.FirstOrDefault(o => o != null && o.regime == regime);

        /// <summary>Primera red terrena que da cobertura a la órbita (sugerencia por defecto).</summary>
        public GroundNetworkSO DefaultNetworkFor(OrbitRegime regime) =>
            groundNetworks.FirstOrDefault(n => n != null && n.Supports(regime));

        public IEnumerable<SpacecraftComponentSO> AllComponents =>
            buses.Cast<SpacecraftComponentSO>()
                .Concat(solarArrays).Concat(batteries).Concat(instruments).Concat(antennas)
                .Concat(computers).Concat(attitudeControls)
                .Where(c => c != null);

        public IEnumerable<SpacecraftComponentSO> ComponentsForRole(MissionRole role) =>
            AllComponents.Where(c => c.OwnerRole == role);
    }
}
