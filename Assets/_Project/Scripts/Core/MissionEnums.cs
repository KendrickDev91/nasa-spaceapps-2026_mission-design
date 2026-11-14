namespace MissionDesign.Core
{
    /// <summary>Regímenes orbitales soportados por el juego.</summary>
    public enum OrbitRegime
    {
        LEO,
        MEO,
        GEO,
        Lunar,
        Interplanetary,
        /// <summary>Sobrevuelo lunar con retorno libre (tipo Apolo 13 / Artemis II): ida, vuelta y amerizaje.</summary>
        LunarFlyby
    }

    /// <summary>Categoría de pieza del ensamblador modular.</summary>
    public enum ComponentCategory
    {
        Bus,
        SolarArray,
        Battery,
        Instrument,
        Antenna,
        OnboardComputer,
        AttitudeControl
    }

    /// <summary>
    /// Básico: piezas esenciales, 1 evento, sin fallos de lanzamiento.
    /// Avanzado: añade computadora de a bordo (memoria, radiación) y control de orientación.
    /// </summary>
    public enum DifficultyLevel
    {
        Basic,
        Advanced
    }

    /// <summary>Puntos de anclaje (sockets) del ModularSpacecraftAssembler.</summary>
    public enum SpacecraftSocket
    {
        CoreBus,
        TopPayloadSocket,
        SolarWingLeft,
        SolarWingRight,
        AntennaSocket,
        ThrusterSocket,
        InternalBay
    }

    public enum InstrumentType
    {
        MultispectralImager,
        Spectrometer,
        Radar,
        Lidar,
        Camera,
        Radiometer
    }

    public enum CommBand
    {
        UHF,
        S,
        X,
        Ka,
        Optical
    }

    /// <summary>Los 4 roles del modo cooperativo (hot-seat).</summary>
    public enum MissionRole
    {
        MissionDirector,
        SystemsEngineer,
        PrincipalScientist,
        TelecomLaunchEngineer
    }

    public enum MissionPhase
    {
        Design,
        Launch,
        OrbitInsertion,
        ScienceOperations,
        DataTransmission,
        /// <summary>Misiones con retorno: reentrada, amerizaje y recuperación de la cápsula y sus datos.</summary>
        ReturnAndRecovery,
        PostMission
    }

    public enum RandomEventType
    {
        SolarStorm,
        MicrometeoroidImpact,
        OptimalObservationWindow,
        GroundStationOutage,
        SingleEventUpset
    }

    /// <summary>
    /// Qué tan confiable es un dato numérico. Se muestra en la UI/reporte para ser
    /// transparentes con el jurado sobre qué viene de fuentes públicas y qué es de diseño de juego.
    /// </summary>
    public enum DataConfidence
    {
        Official,      // Publicado por NASA / operador
        Approximate,   // Derivado o redondeado de fuentes públicas
        GameEstimate   // Valor plausible de diseño de juego, a validar
    }
}
