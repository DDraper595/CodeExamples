using UnityEngine;
using UnityEngine.AI;



public class JumpState : TraverseState
{
    [SerializeField]
    private float jumpVelocity = 10;
    [SerializeField]
    private float gravity = -15f;

    private Vector3 startPoint;
    private Vector3 midPoint;
    private Vector3 endPoint;

    private float velocity = 0;
    private float distanceToMid = 0;
    private float distanceToEnd = 0;
    private float travelledMid = 0;
    private float travelledEnd = 0;

    private int animIDJumping = Animator.StringToHash("jumping");

    protected override void OnEnterState()
    {
        base.OnEnterState();

        travelledMid = 0;
        travelledEnd = 0;
        velocity = jumpVelocity;
        agent.updateRotation = false;

        startPoint = parentObject.transform.position;

        // check if following a jump point or parkour
        if (agent.isOnOffMeshLink)
        {
            OffMeshLinkData data = agent.currentOffMeshLinkData;
            endPoint = data.endPos;
        }
        else
        {
            endPoint = actorMovement.TargetPosition;

            //willStun = (start.y - end.y) > 5;
        }

        // make sure end point is valid
        if (agent.SamplePosition(endPoint, out NavMeshHit navHit, 0.5f, new NavMeshQueryFilter { areaMask = NavMesh.AllAreas, agentTypeID = agent.agentTypeID }))
        {
            endPoint = navHit.position;
        }

        midPoint = Vector3.Lerp(startPoint, endPoint, 0.5f);
        midPoint.y = startPoint.y + 1f;

        distanceToMid = Vector3.Distance(startPoint, midPoint);
        distanceToEnd = Vector3.Distance(midPoint, endPoint);

        Vector3 lookat = endPoint;
        lookat.y = parentObject.transform.position.y;
        parentObject.transform.LookAt(lookat);
    }

    protected override void OnEnterStateRender()
    {
        base.OnEnterStateRender();

        if (actorAnimator)
        {
            actorAnimator.SetBool(animIDJumping, true);
        }
    }

    protected override void OnFixedUpdate()
    {
        base.OnFixedUpdate();

        if (!Object.HasStateAuthority)
            return;

        if (travelledMid < distanceToMid)
        {
            travelledMid += Time.deltaTime * velocity;

            if (velocity > 10)
                velocity += gravity * Time.deltaTime;

            agent.transform.position = Vector3.Lerp(startPoint, midPoint, travelledMid / distanceToMid);
        }

        if (travelledMid >= distanceToMid && travelledEnd < distanceToEnd)
        {
            travelledEnd += Time.deltaTime * velocity;

            if (velocity < 200)
                velocity -= gravity * Time.deltaTime;

            agent.transform.position = Vector3.Lerp(midPoint, endPoint, travelledEnd / distanceToEnd);
        }

        if (travelledEnd >= distanceToEnd)
        {
            agent.updateRotation = true;
            agent.transform.position = endPoint;

            if (agent.isOnOffMeshLink)
                agent.CompleteOffMeshLink();
            else
                agent.Warp(endPoint);

            // todo: stunned

            Machine.TryActivateState(moveState.StateId);
        }
    }

    protected override void OnExitStateRender()
    {
        base.OnExitStateRender();

        if (actorAnimator)
            actorAnimator.SetBool(animIDJumping, false);
    }
}