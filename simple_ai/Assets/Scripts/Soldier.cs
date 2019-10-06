using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI; //for NavMesh

public class Soldier : MonoBehaviour
{
    CoverManager coverManager;

    Soldier curTarget;
    public Team myTeam;
    public Vitals myVitals;

    Transform myTransform;
    public Transform eyes;

    Animator anim;

    [SerializeField] float minAttackDistance = 10, maxAttackDistance = 25, moveSpeed = 15;

    [SerializeField] float damageDealt = 50F;
    [SerializeField] float fireCooldown = 1F;
    float curFireCooldown = 0;

    Vector3 targetLastKnownPosition;
    Path currentPath = null;

    CoverSpot currentCover = null;
    float coverChangeCooldown = 5;
    float curCoverChangeCooldown;

    public enum ai_states
    {
        idle,
        moveToCover,
        combat,
        investigate
    }
    public ai_states state = ai_states.idle;

    // Start is called before the first frame update
    void Start()
    {
        myTransform = transform;
        myTeam = GetComponent<Team>();
        myVitals = GetComponent<Vitals>();
        anim = GetComponent<Animator>();

        coverManager = GameObject.FindObjectOfType<CoverManager>();
        curCoverChangeCooldown = coverChangeCooldown;
    }

    // Update is called once per frame
    void Update()
    {
        if(myVitals.getCurHealth() > 0)
        { 
            switch(state)
            {
                case ai_states.idle:
                    stateIdle();
                    break;
                case ai_states.moveToCover:
                    stateMoveToCover();
                    break;
                case ai_states.combat:
                    stateCombat();
                    break;
                case ai_states.investigate:
                    stateInvestigate();
                    break;
                default:
                    break;
            }
        }
        else
        {
            anim.SetBool("move", false);

            //to be able to investigate last known position of dead soldiers, we'll need to implement dead, instead of destroying the soldier gameobject
            if(GetComponent<BoxCollider>() != null)
            {
                Destroy(GetComponent<BoxCollider>());
            }

            if(currentCover != null)
            {
                coverManager.ExitCover(currentCover);
            }

            Quaternion deathRotation = Quaternion.Euler(90, myTransform.rotation.eulerAngles.y, myTransform.rotation.eulerAngles.z);
            if(myTransform.rotation != deathRotation)
            {
                myTransform.rotation = deathRotation; //maybe it will work?
            }
        }
    }

    void stateIdle()
    {
        if(curTarget != null && curTarget.GetComponent<Vitals>().getCurHealth() > 0)
        {
            if(currentCover != null)
            {
                coverManager.ExitCover(currentCover);
            }

            currentCover = coverManager.GetCoverTowardsTarget(this, curTarget.transform.position, maxAttackDistance, minAttackDistance, currentCover);

            if(currentCover != null)
            {
                if(Vector3.Distance(myTransform.position, currentCover.transform.position) > 0.2F)
                {
                    currentPath = CalculatePath(myTransform.position, currentCover.transform.position);

                    anim.SetBool("move", true);

                    state = ai_states.moveToCover;
                }
                else
                {
                    state = ai_states.combat;
                }
            }
            else
            {
                if (Vector3.Distance(myTransform.position, curTarget.transform.position) <= maxAttackDistance && Vector3.Distance(myTransform.position, curTarget.transform.position) >= minAttackDistance)
                {
                    //attack
                    state = ai_states.combat;
                }
            }
        }
        else
        {
            //find new target
            Soldier bestTarget = GetNewTarget();

            if(bestTarget != null)
            {
                curTarget = bestTarget;
            }
        }
    }
    void stateMoveToCover()
    {
        if(curTarget != null && currentCover != null && currentCover.AmICoveredFrom(curTarget.transform.position))
        {
            if (currentPath != null)
            {
                Soldier alternativeTarget = GetNewTarget();

                if(alternativeTarget != null && alternativeTarget != curTarget)
                {
                    float distanceToCurTarget = Vector3.Distance(myTransform.position, curTarget.transform.position);
                    float distanceToAlternativeTarget = Vector3.Distance(myTransform.position, alternativeTarget.transform.position);
                    float distanceBetweenTargets = Vector3.Distance(curTarget.transform.position, alternativeTarget.transform.position);

                    if(Mathf.Abs(distanceToAlternativeTarget-distanceToCurTarget) > 5 && distanceBetweenTargets > 5)
                    {
                        curTarget = alternativeTarget;
                        coverManager.ExitCover(currentCover);
                        currentCover = coverManager.GetCoverTowardsTarget(this, curTarget.transform.position, maxAttackDistance, minAttackDistance, currentCover);
                        currentPath = CalculatePath(myTransform.position, currentCover.transform.position);
                        return;
                    }
                }

                if (currentPath.ReachedEndNode())
                { //if we reached the end, we'll start looking for a target
                    anim.SetBool("move", false);

                    currentPath = null;

                    state = ai_states.combat;
                    return;
                }

                Vector3 nodePosition = currentPath.GetNextNode();

                if (Vector3.Distance(myTransform.position, nodePosition) < 1)
                {
                    //if we reached the current node, then we'll begin going towards the next node
                    currentPath.currentPathIndex++;
                }
                else
                {
                    //else we'll move towards current node
                    myTransform.LookAt(nodePosition);
                    myTransform.Translate(Vector3.forward * moveSpeed * Time.deltaTime);
                }

            }
            else
            {
                //if we don't have a path, we'll look for a target
                anim.SetBool("move", false);

                state = ai_states.idle;
            }
        }
        else
        {
            anim.SetBool("move", false);

            state = ai_states.idle;
        }
    }

    void stateCombat()
    {
        if (curTarget != null && curTarget.GetComponent<Vitals>().getCurHealth() > 0)
        {
            //if the target escapes during combat
            if (!canISeeTheTarget(curTarget))
            {
                Soldier alternativeTarget = GetNewTarget();

                if(alternativeTarget == null)
                {
                    //If I can't see the target anymore, I'll need to Investigate last known position
                    targetLastKnownPosition = curTarget.transform.position;

                    //we'll need to calculate a path towards the target's last known position and we'll do so using the Unity NavMesh combined with some custom code
                    currentPath = CalculatePath(myTransform.position, targetLastKnownPosition);
                    anim.SetBool("move", true);

                    if(currentCover != null)
                    {
                        coverManager.ExitCover(currentCover);
                    }
                    state = ai_states.investigate;
                }
                else
                {
                    curTarget = alternativeTarget;
                }
                return;
            }

            myTransform.LookAt(curTarget.transform);

            if (Vector3.Distance(myTransform.position, curTarget.transform.position) <= maxAttackDistance && Vector3.Distance(myTransform.position, curTarget.transform.position) >= minAttackDistance)
            {
                //attack
                if(curFireCooldown <= 0)
                {
                    anim.SetTrigger("fire");

                    curTarget.GetComponent<Vitals>().getHit(damageDealt);

                    curFireCooldown = fireCooldown;
                }
                else
                {
                    curFireCooldown -= 1 * Time.deltaTime;
                }
            }
            else
            {
                if(curCoverChangeCooldown <= 0)
                {
                    curCoverChangeCooldown = coverChangeCooldown;
                    anim.SetBool("move", false);

                    state = ai_states.idle;
                }
                else
                {
                    curCoverChangeCooldown -= 1 * Time.deltaTime;
                }
            }
        }
        else
        {
            state = ai_states.idle;
        }
    }

    void stateInvestigate()
    {
        if(currentPath != null)
        {
            Soldier alternativeTarget = GetNewTarget();

            if(currentPath.ReachedEndNode() || alternativeTarget != null)
            { //if we reached the end, we'll start looking for a target
                anim.SetBool("move", false);

                currentPath = null;
                curTarget = alternativeTarget;

                state = ai_states.idle;
                return;
            }

            Vector3 nodePosition = currentPath.GetNextNode();

            if(Vector3.Distance(myTransform.position, nodePosition) < 1)
            {
                //if we reached the current node, then we'll begin going towards the next node
                currentPath.currentPathIndex++;
            }
            else
            {
                //else we'll move towards current node
                myTransform.LookAt(nodePosition);
                myTransform.Translate(Vector3.forward * moveSpeed * Time.deltaTime);
            }

        }
        else
        {
            //if we don't have a path, we'll look for a target
            anim.SetBool("move", false);

            currentPath = null;
            curTarget = null;

            state = ai_states.idle;
        }
    }

    Soldier GetNewTarget()
    {
        Soldier[] allSoldiers = GameObject.FindObjectsOfType<Soldier>();
        Soldier bestTarget = null;
        for (int i = 0; i < allSoldiers.Length; i++)
        {
            Soldier curSoldier = allSoldiers[i];

            //only select current soldier as target, if we are not on the same team and if it got health left
            if (curSoldier.GetComponent<Team>().getTeamNumber() != myTeam.getTeamNumber() && curSoldier.GetComponent<Vitals>().getCurHealth() > 0)
            {
                //if the raycast hit the target, then we know that we can see it
                if (canISeeTheTarget(curSoldier))
                {
                    if (bestTarget == null)
                    {
                        bestTarget = curSoldier;
                    }
                    else
                    {
                        //if current soldier is closer than best target, then choose current soldier as best target
                        if (Vector3.Distance(curSoldier.transform.position, myTransform.position) < Vector3.Distance(bestTarget.transform.position, myTransform.position))
                        {
                            bestTarget = curSoldier;
                        }
                    }
                }
            }
        }

        return bestTarget;
    }

    bool canISeeTheTarget(Soldier target)
    {
        bool canSeeIt = false;

        //Can I see the Target Soldier?
        
        Vector3 enemyPosition = target.eyes.position;

        Vector3 directionTowardsEnemy = enemyPosition - eyes.position;

        RaycastHit hit; //record of what we hit with the raycast

        //cast ray towards current soldier, make the raycast line infinity in length
        if (Physics.Raycast(eyes.position, directionTowardsEnemy, out hit, Mathf.Infinity))
        {
            //if the raycast hit the target, then we know that we can see it
            if (hit.transform == target.transform)
            {
                canSeeIt = true;
            }
        }

        return canSeeIt;
    }

    Path CalculatePath(Vector3 source, Vector3 destination)
    {
        NavMeshPath nvPath = new NavMeshPath();
        NavMesh.CalculatePath(source, destination, NavMesh.AllAreas, nvPath); //calculates a path using the Unity NavMesh

        Path path = new Path(nvPath.corners);

        return path;
    }
}
