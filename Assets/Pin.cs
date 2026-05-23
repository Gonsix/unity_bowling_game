using UnityEngine;

public class Pin : MonoBehaviour
{
    public bool IsStanding()
    {
        // ピンが立っているかどうかを判定するロジックをここに実装
        // 立っている状態と、現在の状態の内積をとる。　現在の状態が立っていれば内積は１に近い値になる。　
        // ピンがぐらつくことだけの場合もあるから、閾値は多分必要　コサイン45度=0.707くらいでいいと思う
        return Vector3.Dot(transform.up, Vector3.up) > 0.707f;
    }
}
