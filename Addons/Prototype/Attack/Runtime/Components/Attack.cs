namespace ME.BECS.Attack {

    public struct AttackComponent : IConfigComponent {

        public float attackRangeSqr;
        
        public float reloadTime;
        internal float reloadTimer;
        
        public float fireTime;
        internal float fireTimer;

        public Config bulletConfig;
        public ME.BECS.Views.View bulletView;
        public ME.BECS.Views.View muzzleView;

    }
    
    public struct CanFireWhileMovesTag : IConfigComponent {}

}