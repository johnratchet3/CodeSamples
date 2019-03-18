using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MegaMiniGun : WeaponModule {
    private bool fireLeft;
    private ParticleSystem ps;
    private bool psEnabled;
    private float turnRate;

    // Use this for initialization
    protected override void Start () {
        base.Start();
        spooling = false;
        spoolDelay = 2.5f;
        spoolTime = 0.0f;
        shootDelay = 0.03f; //every frame. we now alternate left and right barrel.
        fireLeft = true;
        ps = GetComponent<ParticleSystem>();
        psEnabled = false;
        turnRate = 50.0f;
        spread = 12.0f; //buffed due to high rank.
        weaponId = Global.MEGAMINIGUN;
        stage = 5; //change to 6?

        //caching //copy me to any script that triggers a sound
        audioManager = AudioManager.instance;
    }
    
    protected override void lookAtMouse() {
        transform.localScale = new Vector3(1, 1, 1);
        shootTime += Time.deltaTime;
        Vector3 targetPos = new Vector3(Camera.main.ScreenToWorldPoint(Input.mousePosition).x, Camera.main.ScreenToWorldPoint(Input.mousePosition).y, 0.0f);
        Vector3 localPos = new Vector3(transform.position.x, transform.position.y, 0.0f);
        Vector3 vectorToTarget = targetPos - localPos;
        float angle = Mathf.Atan2(vectorToTarget.y, vectorToTarget.x) * Mathf.Rad2Deg - 90;
        Quaternion q = Quaternion.AngleAxis(angle, Vector3.forward);
        if (spoolTime < spoolDelay) {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, q, Time.deltaTime * turnRate * 5.0f);
        }
        else { //turn toward mouse at flat speed.

            transform.rotation = Quaternion.RotateTowards(transform.rotation, q, Time.deltaTime * turnRate);
            //this took far, far too much googling, and I'm pretty sure it's really similar to the wep module one if I think about.
        }
    }
    // Update is called once per frame
    protected override void Update () {
        base.Update();
        //updates every frame. FixedUpdate() updates in fixed intervals (project dependent, tied to physics).
        //we will be decrementing the spoolTime in here. it's a little dirty; we'll be checking if the fire button is held down.
        if (spooling) {//if spooling is true
            if(spoolTime > 0.0f) { //if weapon is still spinning
                if(Input.GetKey(KeyCode.Mouse0) == false) { //if we're not trying to fire...
                    spoolTime -= Time.deltaTime; //slow down the spin / spoolTime
                }
            }
            else { //if spoolTime is < 0 AND we're not trying to shoot;
                spooling = false;
            }

            UpdateParticles();
        }
	}

    private void UpdateParticles() { // Particles synced to operation; while firing, emit the casing particles.
        if (!psEnabled) {
            if(spoolTime > spoolDelay) {
                ps.Play();
                psEnabled = true;
            }
        }
        else if (psEnabled) {
            if(spoolTime < spoolDelay) {
                ps.Stop();
                psEnabled = false;
            }
        }
    }

    public override void Shoot() {
        //main function that activates when lmb is held.
        spooling = true;
        if(spoolTime < spoolDelay) {
            spoolTime += Time.deltaTime;
        }
        if(shootTime > shootDelay && spoolTime >= spoolDelay) {
            Dakka();
        }
    }

    private void Dakka() { //dakka is triggered every frame roughly. //Dakka means shoot (warhammer reference).
        //main firing function.
        //for this minigun, we're going to have 2 barrels. We'll be using 2 firing positions therefore.
        //we *may* need to implement gameobject pooling. tbd. if performance sucks, we'll have a go at it.
        shootTime = 0;

        //if we want extra bullets (eg, more than 60 per second) we'll add an extra shot system.
        // oooh, I can make it alternate left and right.
        if (fireLeft) { //left barrel
            audioManager.PlaySound("Megaminigun"); //placed here to play sound half as frequently.
            Instantiate(projectiles[0], shootPos[0].position,
                Quaternion.Euler(0, 0, transform.eulerAngles.z + spread - 2 * UnityEngine.Random.value * spread));
            Instantiate(projectiles[0], shootPos[0].position,
                Quaternion.Euler(0, 0, transform.eulerAngles.z + spread - 2 * UnityEngine.Random.value * spread));
        }
        else {//right barrel.
            audioManager.PlaySound("Megaminigun");
            Instantiate(projectiles[0], shootPos[1].position,
                Quaternion.Euler(0, 0, transform.eulerAngles.z + spread - 2 * UnityEngine.Random.value * spread));
            Instantiate(projectiles[0], shootPos[1].position,
                Quaternion.Euler(0, 0, transform.eulerAngles.z + spread - 2 * UnityEngine.Random.value * spread));
        }
        fireLeft = !fireLeft;
    }

    public override int ReturnId() {
        return Global.MEGAMINIGUN;
    }
}
