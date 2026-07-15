# NetworkProjectileActor

## Role

Network projectile의 simulation과 hit/collision feedback broadcast를 담당합니다. Fusion network tick에서 projectile을 움직이고, VFX는 RPC로 모든 peer에 전달합니다.

## Source

- `src/Assets/_Project/_Scripts/.../NetworkProjectileActor.cs`
