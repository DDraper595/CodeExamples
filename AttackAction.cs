using UnityEngine;

using Fusion;
using Blockborn.Common;

using Holdara.Common;
using System;

[CreateAssetMenu(fileName = "attack", menuName = "Actions/Attack Action")]
public class AttackAction : GameAction
{
    public string hitEffect;

    [NonSerialized]
    private int animIDAttacking = Animator.StringToHash("attacking");

    public override bool Priority(GameObject actor, ActionRaycast raycastHit)
    {
        Actor actorObj = actor.GetComponent<Actor>();
        return InRangeForAction(actorObj.GetAttackRange(), actor, raycastHit.gameObject, out float diff);
    }

    public override Vector3 GetTimings(CombatState state)
    {
        var attacker = state.CurrentActionData.GetActive(_netObject.Runner).GetComponent<Actor>();
        return attacker.equippedWeapon.animTimings;
    }

    public override bool isActionValid(GameObject actorObject, GameObject targetObject, out ValidActionResponse response)
    {
        if (actorObject == targetObject)
        {
            response = new ValidActionResponse()
            {
                response = ActionResponse.InvalidTarget
            };
            return false;
        }

        if (!targetObject.TryGetComponent(out Actor targetActor))
        {
            response = new ValidActionResponse()
            {
                response = ActionResponse.InvalidUser
            };
            return false;
        }

        if (targetActor.Health.HitPoints <= 0)
        {
            response = new ValidActionResponse()
            {
                response = ActionResponse.InvalidTarget
            };
            return false;
        }

        var actor = actorObject.GetComponent<Actor>();

        if ((actor.MainMachine.ActiveState as IdleState) == null)
        {
            response = new ValidActionResponse()
            {
                response = ActionResponse.WrongAnimState
            };
            return false;
        }

        if (actor.WeaponState != WeaponState.Melee)
        {
            response = new ValidActionResponse()
            {
                response = ActionResponse.WrongWeaponState
            };
            return false;
        }

        if (!actor.combatMode)
        {
            response = new ValidActionResponse()
            {
                response = ActionResponse.InvalidUser
            };
            return false;
        }

        if (!InRangeForAction(actor.GetAttackRange(), actorObject, targetObject, out float diff))
        {
            response = new ValidActionResponse()
            {
                response = ActionResponse.OutOfRange,
                value = diff
            };
            return false;
        }

        response = null;
        return true;
    }

    protected override ActionData Prepare(GameObject actor, ActionRaycast raycastHit)
    {
        var actionData = PrepareActionData();

        Actor targetActor = raycastHit.gameObject.GetComponent<Actor>();

        actionData.AddTarget(actor.GetComponent<NetworkObject>());

        if (targetActor != null)
        {
            actionData.AddTarget(targetActor.Object);
        }

        UpdateTargetLock(actor, targetActor.gameObject);

        return actionData;
    }

    public override ActionData ResolveAction(ActionData actionData)
    {
        var attacker = actionData.GetActive(_netObject.Runner).GetComponent<Actor>();

        CombatState combatState = attacker.MainMachine.GetState<CombatState>();
        combatState.SetNextAction(actionData);

        attacker.MainMachine.TryActivateState(combatState.StateId);

        return actionData;
    }

    public override void OnEnterState(CombatState state)
    {
        var actor = state.CurrentActionData.GetActive(state.Object.Runner).GetComponent<Actor>();
        actor.Energy.SetRechargeRate(0.2f); // adjust energy regen while attacking
    }

    public override void OnEnterStateRender(CombatState state)
    {
        // setup attack anim
        var actor = state.CurrentActionData.GetActive(state.Object.Runner).GetComponent<Actor>();
        ActorAnimator actorAnimator = actor.GetComponentInChildren<ActorAnimator>(true);
        if (actorAnimator)
            actorAnimator.SetTrigger(animIDAttacking);
    }

    public override void OnRender(CombatState state)
    {
        var actor = state.CurrentActionData.GetActive(state.Object.Runner);
        var target = state.CurrentActionData.GetTarget(state.Object.Runner);

        if (actor && target)
        {
            Vector3 look = target.transform.position;
            look.y = actor.transform.position.y;
            actor.transform.LookAt(look); // make sure we're looking at the target
        }
    }    

    public override void OnFixedUpdate(CombatState state)
    {
        if (state.ProgressState.IsFinished)
        {
            GameLog.Log("+++AttackAction Resolve START+++");

            // get attacker and defendering stats
            var attacker = state.CurrentActionData.GetActive(state.Object.Runner).GetComponent<Actor>();
            var target = state.CurrentActionData.GetTarget(state.Object.Runner).GetComponent<Actor>();
            var attackerStatline = attacker.GetComponent<StatLine>();
            var defenderStatline = target.GetComponent<StatLine>();

            // get max damage including mods
            int maxDmg = MaxStrength(attackerStatline.Strength());

            if (target.HasEffect("dreamweave"))
            {
                maxDmg = Mathf.FloorToInt(maxDmg * 0.25f);
            }

            // calc hits
            int hits = Hits(maxDmg, attacker, target, attackerStatline.MeleeAccuracy(), defenderStatline.Dodge(), out int misses);

            float dmgToHealth = 0;
            float armourDamage = 0;
            Vector2 elementalShift = Vector3.zero;

            // if the player has armour calc damage based on alignment
            if (target.Armour.ArmourValue > 0)
            {
                GameLog.Log($"Armour Active");
                float damageMisalignment = DamageMisalignment(attacker.equippedWeapon.AttackElement, target.Armour.defaultElementalAlignment);
                float armourStrength = DamageStrength(ElementalStrength(target.Armour.defaultElementalAlignment), ElementalStrength(attacker.equippedWeapon.AttackElement), damageMisalignment);
                armourDamage = ArmourDamage(hits, armourStrength);

                elementalShift = ElementalShift(attacker.equippedWeapon.AttackElement, maxDmg, armourDamage);
            }
            else
            {
                // no armour damage goes to hp
                GameLog.Log($"Armour Disabled - DamageToHp: {hits}");
                dmgToHealth = hits;
                elementalShift = ElementalShift(attacker.equippedWeapon.AttackElement, maxDmg, dmgToHealth);
            }

            attacker.Energy.Spend(-0.5f);

            // check the defender hasn't dodged during windup
            if (InRangeForAction(1.75f, attacker.gameObject, target.gameObject, out float diff))
            {
                // update hp
                target.AddHealthAndElementalDamage(dmgToHealth, armourDamage, elementalShift);

                EventManager.Trigger("AddDamage", target.gameObject, attacker.attackElement);

                HitState hitState = target.MainMachine.GetState<HitState>();

                if (hitState)
                {
                    hitState.hitEffect = hitEffect;
                    target.MainMachine.TryActivateState(hitState.StateId, true);
                }
            }

            GameLog.Log("+++AttackAction Resolve END+++");
        }
    }
}
