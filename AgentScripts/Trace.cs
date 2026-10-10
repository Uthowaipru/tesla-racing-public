using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public static class Trace
{
    public static async Task<string> Fall()
    {
        var car = GameObject.Find("car");
        var rb = car.GetComponent<Rigidbody>();
        var t = car.transform;
        var sb = new StringBuilder();
        sb.AppendLine("startTime=" + Time.time + " runInBG=" + Application.runInBackground + " timeScale=" + Time.timeScale);
        RaycastHit hit;
        bool planeHit = Physics.Raycast(new Vector3(3.70f, 5f, 2.74f), Vector3.down, out hit, 20f);
        sb.AppendLine("raycastDown=" + planeHit + " hitY=" + (planeHit ? hit.point.y.ToString("F3") : "n/a") + " collider=" + (planeHit ? hit.collider.name : "n/a"));

        for (int i = 0; i < 16; i++)
        {
            sb.AppendLine($"t={Time.time:F2} pos={t.position.ToString("F2")} rot={t.rotation.eulerAngles.ToString("F0")} vel={rb.linearVelocity.ToString("F2")} angVel={rb.angularVelocity.ToString("F2")} g={GroundedMask(car)}");
            await Task.Delay(200);
        }
        return sb.ToString();
    }

    static string GroundedMask(GameObject car)
    {
        var vc = car.GetComponent<VehicleSystem.VehicleController>();
        var s = "";
        foreach (var w in vc.wheels) s += (w.collider != null && w.collider.isGrounded ? "1" : "0");
        return s;
    }
}