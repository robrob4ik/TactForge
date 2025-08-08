
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace OneBitRob
{
    [System.Serializable]
    public class TraitIconMapping
    {
        public UnitTrait trait;
        public Sprite icon;
    }

    [System.Serializable]
    public class ClassIconMapping
    {
        public UnitClass unitClass;
        public Sprite icon;
    }
   
    [CreateAssetMenu(fileName = "IconLibrary", menuName = "Game/IconLibrary")]
    public class IconLibrary : SerializedScriptableObject
    {
        [TableList]
        public List<TraitIconMapping> traitIcons = new List<TraitIconMapping>();

        [TableList]
        public List<ClassIconMapping> classIcons = new List<ClassIconMapping>();

        public Sprite GetTraitIcon(UnitTrait trait)
        {
            foreach (var mapping in traitIcons)
            {
                if (mapping.trait == trait)
                    return mapping.icon;
            }
            return null;
        }

        public Sprite GetClassIcon(UnitClass unitClass)
        {
            foreach (var mapping in classIcons)
            {
                if (mapping.unitClass == unitClass)
                    return mapping.icon;
            }
            return null;
        }
    }
}