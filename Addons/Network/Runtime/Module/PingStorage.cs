using UnityEngine;

namespace ME.BECS.Network {
    public class PingStorage {

        private uint[] storage;
        private int rover;
        private uint[] sortedStorage;
        private int storedAmount;
        private int capacity;
        private int medianSize;

        public uint median { get; private set; }
        public uint min { get; private set; }
        public uint max { get; private set; }

        public PingStorage(int capacity = 64, int medianSize = 50) {

            this.storage = new uint[capacity];
            this.rover = 0;
            this.sortedStorage = new uint[capacity];

            this.capacity = capacity;
            this.medianSize = medianSize;
            this.storedAmount = 0;

        }

        public void AddValue(uint val) {

            this.storage[this.rover] = val;
            this.rover = (this.rover + 1) % this.capacity;
            this.storedAmount = Mathf.Min(++this.storedAmount,  this.capacity);

            for (var i = 0; i < this.capacity; ++i) {
                this.sortedStorage[i] = this.storage[i];
            }

            System.Array.Sort(this.sortedStorage);

            uint medianSum = 0;
            // there are zeros in the beginning after sorting. So ignore them
            int zeroShift = this.storedAmount < this.capacity ? this.capacity - this.storedAmount : 0;
            var startPoint = this.storedAmount > this.medianSize ? (this.storedAmount - this.medianSize) / 2 : 0;
            startPoint += zeroShift;
            var amount = this.storedAmount > this.medianSize ? this.medianSize : this.storedAmount;
            for (var i = startPoint; i < amount + startPoint; ++i) {
                medianSum += this.sortedStorage[i];
            }

            this.median = (uint)(medianSum / amount);
            this.min = this.sortedStorage[zeroShift];
            this.max = this.sortedStorage[zeroShift + this.storedAmount - 1];

        }

    }
}
