namespace ME.BECS.Attack {

    public struct AttackComponent : IConfigComponent {

        public float attackRangeSqr;
        
        public float reloadTime;
        internal float reloadTimer;

        public Config bulletConfig;
        public ME.BECS.Views.View bulletView;
        public ME.BECS.Views.View muzzleView;

    }

}