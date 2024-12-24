
namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using static Cuts;

    public unsafe partial struct MemoryAllocator {

        [INLINE(256)]
        public void CopyFrom(in MemoryAllocator other) {

            if (other.zonesList == null && this.zonesList == null) {
                
            } else if (other.zonesList == null && this.zonesList != null) {
                this.FreeZones();
            } else {
	    
		        var areEquals = true;
                
                if (this.zonesListCount < other.zonesListCount) {

                    for (uint i = this.zonesListCount; i < other.zonesListCount; ++i) {
                        var otherZone = other.zonesList[i];
                        if (otherZone == null) {
                            this.AddZone(null, false);
                        } else {
                            var zone = MemoryAllocator.ZmCreateZone(otherZone->size);
                            this.AddZone(zone, false);
                        }
                    }
                        
                }
                
                if (this.zonesListCount == other.zonesListCount) {

                    for (int i = 0; i < other.zonesListCount; ++i) {
                        ref var curZone = ref this.zonesList[i];
                        var otherZone = other.zonesList[i];
                        {
                            if (curZone == null && otherZone == null) continue;
                            
                            if (curZone == null) {
                                curZone = MemoryAllocator.ZmCreateZone(otherZone->size);
                                _memcpy((safe_ptr)otherZone, (safe_ptr)curZone, otherZone->size);
                            } else if (otherZone == null) {
                                MemoryAllocator.ZmFreeZone(curZone);
                                curZone = null;
                            } else {
                                // resize zone
                                curZone = MemoryAllocator.ZmReallocZone(curZone, otherZone->size);
                                _memcpy((safe_ptr)otherZone, (safe_ptr)curZone, otherZone->size);
                            }
                        }
                    }

                } else {

                    areEquals = false;
                    
                }

                if (areEquals == false) {
		    
                    this.FreeZones();

		            for (int i = 0; i < other.zonesListCount; i++) {
		                var otherZone = other.zonesList[i];

                        if (otherZone != null) {
                            var zone = MemoryAllocator.ZmCreateZone(otherZone->size);
                            _memcpy((safe_ptr)otherZone, (safe_ptr)zone, otherZone->size);
                            this.AddZone(zone, false);
                        } else {
                            this.AddZone(null, false);
                        }

                    }
                    
                }

            }

            this.version = other.version;
            ++this.version;
            this.initialSize = other.initialSize;

        }

        [INLINE(256)]
        public void CopyFromComplete(in MemoryAllocator other, int index) {
            
            // We must be sure that source allocator has the same structure and size as current
            // So we must call CopyFromPrepare() first
            var curZone = this.zonesList[index];
            var otherZone = other.zonesList[index];
            {
                if (curZone == null && otherZone == null) return;
                {
                    _memcpy((safe_ptr)otherZone, (safe_ptr)curZone, otherZone->size);
                }
            }
            
        }

        [INLINE(256)]
        public void CopyFromPrepare(in MemoryAllocator other) {

            if (other.zonesList == null && this.zonesList == null) {
                
            } else if (other.zonesList == null && this.zonesList != null) {
                this.FreeZones();
            } else {
	    
		        var areEquals = true;
                
                if (this.zonesListCount < other.zonesListCount) {

                    for (uint i = this.zonesListCount; i < other.zonesListCount; ++i) {
                        var otherZone = other.zonesList[i];
                        if (otherZone == null) {
                            this.AddZone(null, false);
                        } else {
                            var zone = MemoryAllocator.ZmCreateZone(otherZone->size);
                            this.AddZone(zone, false);
                        }
                    }
                        
                }
                
                if (this.zonesListCount == other.zonesListCount) {

                    for (int i = 0; i < other.zonesListCount; ++i) {
                        ref var curZone = ref this.zonesList[i];
                        var otherZone = other.zonesList[i];
                        {
                            if (curZone == null && otherZone == null) continue;
                            
                            if (curZone == null) {
                                curZone = MemoryAllocator.ZmCreateZone(otherZone->size);
                            } else if (otherZone == null) {
                                MemoryAllocator.ZmFreeZone(curZone);
                                curZone = null;
                            } else {
                                // resize zone
                                curZone = MemoryAllocator.ZmReallocZone(curZone, otherZone->size);
                            }
                        }
                    }

                } else {

                    areEquals = false;
                    
                }

                if (areEquals == false) {
		    
                    this.FreeZones();

		            for (int i = 0; i < other.zonesListCount; i++) {
		                var otherZone = other.zonesList[i];
                        if (otherZone != null) {
                            var zone = MemoryAllocator.ZmCreateZoneEmpty(otherZone->size);
                            this.AddZone(zone, false);
                        } else {
                            this.AddZone(null, false);
                        }

                    }

                }

            }

            this.version = other.version;
            ++this.version;
            this.initialSize = other.initialSize;

        }

    }

}
