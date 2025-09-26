using OneBitRob.AI;
using OneBitRob.AI.OneBitRob.AI;
using Unity.Jobs;

[assembly: RegisterGenericJobType(typeof(CollectRunningJob<SetupUnitComponent, SetupUnitTag>))]

[assembly: RegisterGenericJobType(typeof(CollectRunningJob<FindTargetComponent, FindTargetTag>))]
[assembly: RegisterGenericJobType(typeof(CollectRunningJob<FindTargetInsideBannerAreaComponent, FindTargetInsideBannerAreaTag>))]
[assembly: RegisterGenericJobType(typeof(CollectRunningJob<FindTargetNearbyIfPokeComponent, FindTargetNearbyIfPokeTag>))]

[assembly: RegisterGenericJobType(typeof(CollectRunningJob<AssignBannerComponent, AssignBannerTag>))]
[assembly: RegisterGenericJobType(typeof(CollectRunningJob<MoveToBannerComponent, MoveToBannerTag>))]
[assembly: RegisterGenericJobType(typeof(CollectRunningJob<MoveToObjectiveComponent, MoveToObjectiveTag>))]

[assembly: RegisterGenericJobType(typeof(CollectRunningJob<MoveToTargetComponent, MoveToTargetTag>))]
[assembly: RegisterGenericJobType(typeof(CollectRunningJob<RotateToTargetComponent, RotateToTargetTag>))]
[assembly: RegisterGenericJobType(typeof(CollectRunningJob<AttackTargetComponent, AttackTargetTag>))]

[assembly: RegisterGenericJobType(typeof(CollectRunningJob<CanCastSpellComponent, CanCastSpellTag>))]
[assembly: RegisterGenericJobType(typeof(CollectRunningJob<ReadyToCastSpellComponent, ReadyToCastSpellTag>))]
[assembly: RegisterGenericJobType(typeof(CollectRunningJob<CastSpellComponent, CastSpellTag>))]

[assembly: RegisterGenericJobType(typeof(CollectRunningJob<IsRangedComponent, IsRangedTag>))]
[assembly: RegisterGenericJobType(typeof(CollectRunningJob<IsTargetAliveComponent, IsTargetAliveTag>))]
[assembly: RegisterGenericJobType(typeof(CollectRunningJob<IsMeleeComponent, IsMeleeTag>))]
[assembly: RegisterGenericJobType(typeof(CollectRunningJob<IsAllyComponent, IsAllyTag>))]
[assembly: RegisterGenericJobType(typeof(CollectRunningJob<HasBannerComponent, HasBannerTag>))]
[assembly: RegisterGenericJobType(typeof(CollectRunningJob<IsTargetInAttackRangeComponent, IsTargetInAttackRangeTag>))]
[assembly: RegisterGenericJobType(typeof(CollectRunningJob<IsTargetInsideBannerAreaComponent, IsTargetInsideBannerAreaTag>))]