using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Team : MonoBehaviour
{
    [SerializeField] int teamNumber;

    public int getTeamNumber()
    {
        return teamNumber;
    }
}
