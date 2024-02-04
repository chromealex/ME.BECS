namespace ME.BECS.Views {
    
    using Unity.Mathematics;
    
    public struct CameraComponent : IComponent {

        public MemArrayAuto<UnityEngine.Plane> localPlanes;
        public float nearClipPlane;
        public float farClipPlane;
        public float fieldOfViewVertical;
        public float fieldOfViewHorizontal;
        public float aspect;
        public UnityEngine.Bounds bounds;

    }

    public struct CameraAspect : IAspect {
        
        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<CameraComponent> cameraDataPtr;

        public readonly ref CameraComponent component => ref this.cameraDataPtr.Get(this.ent.id, this.ent.gen);
        public readonly ref readonly CameraComponent readComponent => ref this.cameraDataPtr.Read(this.ent.id, this.ent.gen);
        public readonly ref readonly UnityEngine.Bounds bounds => ref this.cameraDataPtr.Read(this.ent.id, this.ent.gen).bounds;

        public UnityEngine.Bounds WorldBounds => new UnityEngine.Bounds((float3)this.bounds.center + this.ent.GetAspect<ME.BECS.Transforms.TransformAspect>().GetWorldMatrixPosition(), this.bounds.size);

    }
    
}