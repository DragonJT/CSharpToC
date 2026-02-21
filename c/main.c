#include <stdio.h>
#include "raylib.h"
#include "raymath.h"
#define RLIGHTS_IMPLEMENTATION
#include "rlights.h"
int main()
{
SetConfigFlags(FLAG_MSAA_4X_HINT);
InitWindow(800, 600, "Test");
Shader shader = LoadShader("c/lighting.vs", "c/lighting.fs");
shader.locs[SHADER_LOC_VECTOR_VIEW] = GetShaderLocation(shader, "viewPos");
int ambientLoc = GetShaderLocation(shader, "ambient");
SetShaderValue(shader, ambientLoc, (float[4]){0.1, 0.1, 0.1, 1}, SHADER_UNIFORM_VEC4);
Light light = CreateLight(LIGHT_POINT, (Vector3){-5, 2, -5}, (Vector3){0, 0, 0}, YELLOW, shader);
float radians = 0;
while(!WindowShouldClose())
{
Camera3D camera = {0};
camera.position = (Vector3){(cosf(radians) * 10), 10, (sinf(radians) * 10)};
camera.target = (Vector3){0, 0, 0};
camera.up = (Vector3){0, 1, 0};
camera.fovy = 45;
camera.projection = CAMERA_PERSPECTIVE;
UpdateLightValues(shader, light);
float* cameraPos = (float[3]){camera.position.x, camera.position.y, camera.position.z};
SetShaderValue(shader, shader.locs[SHADER_LOC_VECTOR_VIEW], cameraPos, SHADER_UNIFORM_VEC3);
BeginDrawing();
ClearBackground(BLUE);
BeginMode3D(camera);
BeginShaderMode(shader);
DrawCube((Vector3){0, 0, 0}, 4, 1, 4, RED);
DrawCube((Vector3){0, 0, 0}, 2, 2, 2, BLACK);
EndShaderMode();
EndMode3D();
DrawText("HELLO WORLD", 100, 100, 50, RAYWHITE);
EndDrawing();
radians += (PI * GetFrameTime());
}
}
