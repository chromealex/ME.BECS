namespace ME.BECS {

    /// <summary>
    /// If you use Ent.New(..) - this type will be used
    /// </summary>
    public struct DefaultEntityType : IEntityType {}
    
    /// <summary>
    /// Use this type if this entity is single
    /// </summary>
    public struct SingletonEntityType : IEntityType {}
    
    /// <summary>
    /// Use this type if your entities count is less than 32
    /// If not - it will be resized to 64, then to 96, then to 128 etc
    /// </summary>
    public struct Capacity32EntityType : IEntityType {}

}