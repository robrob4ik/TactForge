using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector; // Add this for Odin Inspector support

public class TripoAvatarBuilder : MonoBehaviour
{
    // Attach this to your reference model's root GameObject in the scene.
    // Click the "Build and Save Avatar" button in the Inspector to run.
    // For reuse: Load the saved Avatar asset and assign to other models' Animators.

    [Button("Build and Save Avatar")]
    void BuildAndSaveAvatar()
    {
        // Find root with SkinnedMeshRenderer
        SkinnedMeshRenderer skinnedMesh = GetComponentInChildren<SkinnedMeshRenderer>();
        if (skinnedMesh == null)
        {
            Debug.LogError("No SkinnedMeshRenderer found.");
            return;
        }

        // Auto-map bones using your exact Tripo naming format (case-insensitive partial match)
        Dictionary<HumanBodyBones, Transform> boneMap = new Dictionary<HumanBodyBones, Transform>();
        Transform[] allBones = skinnedMesh.bones;  // Or GetComponentsInChildren<Transform>()

        boneMap[HumanBodyBones.Hips] = FindBone(allBones, "hip") ?? FindBone(allBones, "pelvis");
        boneMap[HumanBodyBones.LeftUpperLeg] = FindBone(allBones, "l_thigh");
        boneMap[HumanBodyBones.RightUpperLeg] = FindBone(allBones, "r_thigh");
        boneMap[HumanBodyBones.LeftLowerLeg] = FindBone(allBones, "l_calf");
        boneMap[HumanBodyBones.RightLowerLeg] = FindBone(allBones, "r_calf");
        boneMap[HumanBodyBones.LeftFoot] = FindBone(allBones, "l_foot");
        boneMap[HumanBodyBones.RightFoot] = FindBone(allBones, "r_foot");
        boneMap[HumanBodyBones.Spine] = FindBone(allBones, "waist") ?? FindBone(allBones, "spine01");
        boneMap[HumanBodyBones.Chest] = FindBone(allBones, "spine02") ?? FindBone(allBones, "chest");
        boneMap[HumanBodyBones.UpperChest] = FindBone(allBones, "upperchest");  // If present; optional
        boneMap[HumanBodyBones.Neck] = FindBone(allBones, "necktwist01") ?? FindBone(allBones, "necktwist02") ?? FindBone(allBones, "neck");
        boneMap[HumanBodyBones.Head] = FindBone(allBones, "head");
        boneMap[HumanBodyBones.LeftShoulder] = FindBone(allBones, "l_clavicle") ?? FindBone(allBones, "l_shoulder");
        boneMap[HumanBodyBones.RightShoulder] = FindBone(allBones, "r_clavicle") ?? FindBone(allBones, "r_shoulder");
        boneMap[HumanBodyBones.LeftUpperArm] = FindBone(allBones, "l_upperarm");
        boneMap[HumanBodyBones.RightUpperArm] = FindBone(allBones, "r_upperarm");
        boneMap[HumanBodyBones.LeftLowerArm] = FindBone(allBones, "l_forearm");
        boneMap[HumanBodyBones.RightLowerArm] = FindBone(allBones, "r_forearm");
        boneMap[HumanBodyBones.LeftHand] = FindBone(allBones, "l_hand");
        boneMap[HumanBodyBones.RightHand] = FindBone(allBones, "r_hand");
        // Optional: Fingers/toes (uncomment and add if in your hierarchy; not shown but common)
        // boneMap[HumanBodyBones.LeftThumbProximal] = FindBone(allBones, "l_thumb1");
        // boneMap[HumanBodyBones.LeftToes] = FindBone(allBones, "l_toe");

        // Build HumanBones array
        List<HumanBone> humanBones = new List<HumanBone>();
        string[] humanBoneNames = HumanTrait.BoneName;
        foreach (var kvp in boneMap.Where(k => k.Value != null))
        {
            humanBones.Add(new HumanBone
            {
                boneName = kvp.Value.name,
                humanName = humanBoneNames[(int)kvp.Key],
                limit = new HumanLimit { useDefaultValues = true }
            });
        }

        // Build full skeleton array including twists (traverse from Armature root to include parents)
        List<SkeletonBone> skeletonBones = new List<SkeletonBone>();
        // Find the 'Armature' transform to start from the top of the bone hierarchy
        Transform armature = transform.Find("Armature");
        Transform startBone = armature != null ? armature : skinnedMesh.rootBone;
        if (startBone != null)
        {
            AddBoneToSkeleton(startBone, skeletonBones);
        }
        else
        {
            Debug.LogWarning("No armature or root bone found; using auto-infer.");
        }

        // Create HumanDescription
        HumanDescription humanDesc = new HumanDescription
        {
            human = humanBones.ToArray(),
            skeleton = skeletonBones.ToArray(),  // Include all for better deformation
            upperArmTwist = 0.5f,  // Default; tweak if ribs twist too much
            lowerArmTwist = 0.5f,
            upperLegTwist = 0.5f,  // For thigh/calf twists
            lowerLegTwist = 0.5f,
            armStretch = 0.05f,
            legStretch = 0.05f,
            feetSpacing = 0f
        };

        // Build Avatar
        Avatar avatar = AvatarBuilder.BuildHumanAvatar(gameObject, humanDesc);
        avatar.name = "TripoSharedAvatar";
        if (avatar.isValid)
        {
            // Assign to this model's Animator (optional)
            Animator animator = GetComponent<Animator>() ?? gameObject.AddComponent<Animator>();
            animator.avatar = avatar;

            // Save as asset for reuse
            AssetDatabase.CreateAsset(avatar, "Assets/TripoSharedAvatar.asset");
            AssetDatabase.SaveAssets();
            Debug.Log("Avatar built and saved as Assets/TripoSharedAvatar.asset. Reuse on other models!");
        }
        else
        {
            Debug.LogError("Invalid Avatar; check console for missing bones and add fallbacks to script.");
        }
    }

    // Helper to recursively add all bones to skeleton
    private void AddBoneToSkeleton(Transform bone, List<SkeletonBone> skeleton)
    {
        if (bone == null) return;

        skeleton.Add(new SkeletonBone
        {
            name = bone.name,
            position = bone.localPosition,
            rotation = bone.localRotation,
            scale = bone.localScale
        });

        foreach (Transform child in bone)
        {
            AddBoneToSkeleton(child, skeleton);
        }
    }

    static Transform FindBone(Transform[] bones, string partialName)
    {
        partialName = partialName.ToLower();
        return bones.FirstOrDefault(t => t.name.ToLower().Contains(partialName));
    }
}