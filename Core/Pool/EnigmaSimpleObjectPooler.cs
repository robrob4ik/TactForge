// File: Assets/AssetsWithoutIndex/TopDownEngine/ThirdParty/MoreMountains/MMTools/Core/MMObjectPool/MMSimpleObjectPooler.cs
using UnityEngine;
using System.Collections.Generic;
using MoreMountains.Tools;
using UnityEngine.SceneManagement;

namespace OneBitRob.Tools
{
    /// A simple object pool outputting a single type of objects
    [AddComponentMenu("Enigma Simple Object Pooler")]
    public class EnigmaSimpleObjectPooler : MMObjectPooler
    {
        /// the game object we'll instantiate
        public GameObject GameObjectToPool;
        /// the number of objects we'll add to the pool
        public int PoolSize = 20;
        /// if true, the pool will automatically add objects to the itself if needed
        public bool PoolCanExpand = true;

        public virtual List<EnigmaSimpleObjectPooler> Owner { get; set; }
        private void OnDestroy() { Owner?.Remove(this); }

        /// Fills the object pool with the gameobject type you've specified in the inspector
        public override void FillObjectPool()
        {
            if (GameObjectToPool == null)
            {
                return;
            }

            // If we've already created a pool, we exit
            if ((_objectPool != null) && (_objectPool.PooledGameObjects.Count > PoolSize))
            {
                return;
            }

            CreateWaitingPool();

            int objectsToSpawn = PoolSize;

            if (_objectPool != null)
            {
                objectsToSpawn -= _objectPool.PooledGameObjects.Count;
            }

            // we add to the pool the specified number of objects
            for (int i = 0; i < objectsToSpawn; i++)
            {
                AddOneObjectToThePool();
            }
        }

        /// Determines the name of the object pool.
        protected override string DetermineObjectPoolName()
        {
            return ("[SimpleObjectPooler] " + GameObjectToPool.name);
        }

        /// Public: prune destroyed entries (called by PoolHub on repair)
        public void PruneDestroyedEntries()
        {
            if (_objectPool == null) return;
            var list = _objectPool.PooledGameObjects;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] == null)
                {
                    list.RemoveAt(i);
                }
            }
        }

        /// This method returns one inactive object from the pool
        public override GameObject GetPooledGameObject()
        {
            // Ensure the pool exists
            if (_objectPool == null)
            {
                FillObjectPool();
                if (_objectPool == null)
                {
                    return null;
                }
            }

            // Cull destroyed entries to avoid MissingReferenceException on .activeInHierarchy
            PruneDestroyedEntries();

            var list = _objectPool.PooledGameObjects;

            // we go through the pool looking for an inactive object
            for (int i = 0; i < list.Count; i++)
            {
                var go = list[i];
                if (go != null && !go.activeInHierarchy)
                {
                    // if we find one, we return it
                    return go;
                }
            }
            // if we haven't found an inactive object (the pool is empty), and if we can extend it, we add one new object to the pool, and return it
            if (PoolCanExpand)
            {
                return AddOneObjectToThePool();
            }
            // if the pool is empty and can't grow, we return nothing.
            return null;
        }

        /// Adds one object of the specified type (in the inspector) to the pool.
        protected virtual GameObject AddOneObjectToThePool()
        {
            if (GameObjectToPool == null)
            {
                Debug.LogWarning("The " + gameObject.name + " ObjectPooler doesn't have any GameObjectToPool defined.", gameObject);
                return null;
            }

            bool initialStatus = GameObjectToPool.activeSelf;
            GameObjectToPool.SetActive(false);
            GameObject newGameObject = (GameObject)Instantiate(GameObjectToPool);
            GameObjectToPool.SetActive(initialStatus);
            SceneManager.MoveGameObjectToScene(newGameObject, this.gameObject.scene);
            if (NestWaitingPool && _waitingPool != null)
            {
                newGameObject.transform.SetParent(_waitingPool.transform);
            }
            newGameObject.name = GameObjectToPool.name + "-" + _objectPool.PooledGameObjects.Count;

            _objectPool.PooledGameObjects.Add(newGameObject);

            return newGameObject;
        }
    }
}

