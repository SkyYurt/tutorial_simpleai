using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Vitals : MonoBehaviour
{
    [SerializeField] float health = 100;
    float curHealth = 100;

    // Start is called before the first frame update
    void Start()
    {
        curHealth = health;    
    }

    public float getCurHealth()
    {
        return curHealth;
    }

    public void getHit(float damage)
    {
        curHealth -= damage;
    }
}
