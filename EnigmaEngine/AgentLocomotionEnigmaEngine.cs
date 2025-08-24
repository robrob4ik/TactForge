using OneBitRob;
using OneBitRob.EnigmaEngine;
using ProjectDawn.Entities;
using ProjectDawn.Navigation;
using ProjectDawn.Navigation.Hybrid;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(AgentAuthoring))]
public class AgentLocomotionEnigmaEngine : MonoBehaviour
{
    private UnitDefinition _unitDefinition;

    public AgentLocomotion Locomotion = AgentLocomotion.Default;

    AgentAuthoring m_Agent;
    AgentCylinderShapeAuthoring _cylinderShape;
    EnigmaCharacterMovement _characterMovement;

    private Entity _agentEntity;

    ref AgentBody Body => ref World.DefaultGameObjectInjectionWorld.EntityManager
        .GetComponentDataRW<AgentBody>(_agentEntity).ValueRW;

    void OnEnable()
    {
        _unitDefinition = GetComponent<UnitDefinitionProvider>().unitDefinition;
        m_Agent = GetComponent<AgentAuthoring>();
        _cylinderShape = GetComponent<AgentCylinderShapeAuthoring>();
        _characterMovement = gameObject.GetComponent<EnigmaCharacter>()?.FindAbility<EnigmaCharacterMovement>();

        _agentEntity = m_Agent.GetOrCreateEntity(); // cache to avoid repeated lookups
    }

    void FixedUpdate()
    {
        ref AgentBody body = ref Body;

        if (body.IsStopped)
            return;

        float3 towards = body.Destination - (float3)transform.position;
        float distance = math.length(towards);
        float3 desiredDirection = distance > math.EPSILON ? towards / distance : float3.zero;

        body.Force = desiredDirection;
        body.RemainingDistance = distance;
    }

    void LateUpdate()
    {
        ref AgentBody body = ref Body;
        float DeltaTime = Time.deltaTime;
        AgentLocomotion locomotion = Locomotion;
        AgentShape shape = _cylinderShape.EntityShape;

        if (body.IsStopped)
        {
            _characterMovement.SetMovement(Vector2.zero);
            return;
        }

        float remainingDistance = body.RemainingDistance;
        if (remainingDistance <= _unitDefinition.stoppingDistance + 1e-3f)
        {
            body.IsStopped = true;
            body.Velocity = 0;
            _characterMovement.SetMovement(Vector2.zero);
            return;
        }

        float maxSpeed = _unitDefinition.moveSpeed;

        if (locomotion.AutoBreaking)
        {
            float breakDistance = shape.Radius * 2 + _unitDefinition.stoppingDistance;
            if (remainingDistance <= breakDistance)
            {
                maxSpeed = math.lerp(_unitDefinition.moveSpeed * 0.25f, _unitDefinition.moveSpeed, remainingDistance / breakDistance);
            }
        }

        float forceLength = math.length(body.Force);
        if (forceLength > 1)
            body.Force = body.Force / forceLength;

        body.Velocity = math.lerp(body.Velocity, body.Force * maxSpeed, math.saturate(DeltaTime * _unitDefinition.acceleration));

        Vector2 moveDirection2 = new Vector2(body.Velocity.x, body.Velocity.z);
        _characterMovement.SetMovement(moveDirection2);
    }
}
