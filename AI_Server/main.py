import numpy as np
from fastapi import FastAPI, Response
import uvicorn
import random
import heapq
import matplotlib.pyplot as plt
import io

app = FastAPI()

class IndustrialShapeGenerator:
    def __init__(self):
        self.VOID, self.FLOOR = 0, 1
        self.WALL_STRAIGHT = 2
        self.CORNER_EXT, self.CORNER_INT = 3, 4
        self.DOOR_IN, self.DOOR_OUT = 5, 6
        self.WALL_PLACEHOLDER = 7
        self.ROBOT_ARM = 8 
        self.CONVEYOR_SOL = 9 

        # COSTURI FIXE
        self.COST_CONVEYOR = 100
        self.COST_ROBOT = 2000

        self.colors = {
            0: [1.0, 1.0, 1.0], 1: [0.2, 0.4, 1.0], 2: [0.0, 0.0, 0.0],
            3: [1.0, 0.5, 0.0], 4: [0.8, 0.0, 0.8], 5: [0.0, 1.0, 0.0],
            6: [1.0, 0.0, 0.0], 7: [0.7, 0.7, 0.7], 8: [1.0, 1.0, 0.0],
            9: [1.0, 0.0, 1.0]
        }

    def get_distance(self, p1, p2):
        return abs(p1[0] - p2[0]) + abs(p1[1] - p2[1])

    def get_free_tiles(self, grid):
        free_tiles = []
        rows, cols = grid.shape
        doors = np.argwhere((grid == self.DOOR_IN) | (grid == self.DOOR_OUT))
        for dr, dc in doors:
            for nr, nc in [(dr-1, dc), (dr+1, dc), (dr, dc-1), (dr, dc+1)]:
                if 0 <= nr < rows and 0 <= nc < cols and grid[nr, nc] == self.FLOOR:
                    free_tiles.append((nr, nc))
        return free_tiles

    def find_optimal_path(self, grid):
        rows, cols = grid.shape
        start_nodes = np.argwhere(grid == self.DOOR_IN)
        end_nodes = np.argwhere(grid == self.DOOR_OUT)
        if not len(start_nodes) or not len(end_nodes): return []
        
        start, end = tuple(start_nodes[0]), tuple(end_nodes[0])
        free_tiles = self.get_free_tiles(grid)

        # pq: (cost_estimat, r, c, last_dr, last_dc, path)
        pq = [(0, start[0], start[1], 0, 0, [start])]
        visited = {}

        while pq:
            cost, r, c, dr, dc, path = heapq.heappop(pq)
            if (r, c) == end: return path
            
            state = (r, c, dr, dc)
            if state in visited and visited[state] <= cost: continue
            visited[state] = cost

            for ndr, ndc in [(-1, 0), (1, 0), (0, -1), (0, 1)]:
                nr, nc = r + ndr, c + ndc
                if 0 <= nr < rows and 0 <= nc < cols:
                    if grid[nr, nc] in [self.FLOOR, self.ROBOT_ARM, self.DOOR_OUT]:
                        is_free = (nr, nc) in free_tiles
                        # Cost simulit pentru A* (ca să găsească drumul bun)
                        step_cost = 0 if is_free else self.COST_CONVEYOR
                        if (dr, dc) != (0, 0) and (dr, dc) != (ndr, ndc):
                            if grid[r, c] != self.ROBOT_ARM and not is_free:
                                step_cost += self.COST_ROBOT
                        
                        heapq.heappush(pq, (cost + step_cost, nr, nc, ndr, ndc, path + [(nr, nc)]))
        return []

    def process_ai_solution(self, matrix, path):
        ai_sol = np.zeros((25, 25), dtype=int)
        free_tiles = self.get_free_tiles(matrix)
        paid_conv, paid_rob = 0, 0
        
        if len(path) < 3: return ai_sol, 0, 0

        # Identificăm direcțiile pentru a pune roboți la viraje
        for i in range(1, len(path) - 1):
            r, c = path[i]
            prev_r, prev_c = path[i-1]
            next_r, next_c = path[i+1]
            
            dir_in = (r - prev_r, c - prev_c)
            dir_out = (next_r - r, next_c - c)
            
            is_free = (r, c) in free_tiles

            # Decidem ce piesă punem
            if dir_in != dir_out and dir_in != (0,0) and not is_free:
                # E viraj și nu e moca -> ROBOT
                ai_sol[r, c] = self.ROBOT_ARM
                if matrix[r, c] != self.ROBOT_ARM: # Plătim doar dacă nu e robot de generare
                    paid_rob += 1
            else:
                # E linie dreaptă sau e robot existent pe linie dreaptă sau e moca
                if matrix[r, c] == self.ROBOT_ARM and not is_free:
                    ai_sol[r, c] = self.ROBOT_ARM # Rămâne vizual robot, dar e "free"
                else:
                    ai_sol[r, c] = self.CONVEYOR_SOL
                    if not is_free:
                        paid_conv += 1

        # Calculăm bugetul EXPLICIT bazat pe numărătoare
        base_budget = (paid_conv * self.COST_CONVEYOR) + (paid_rob * self.COST_ROBOT)
        return ai_sol, paid_conv, paid_rob, base_budget

    # --- Generarea halei (neschimbată) ---
    def apply_smart_walls(self, grid):
        rows, cols = grid.shape
        new_grid = grid.copy()
        for r in range(rows):
            for c in range(cols):
                if grid[r, c] == self.VOID:
                    f_card = sum(1 for dr, dc in [(-1,0),(1,0),(0,-1),(0,1)] if 0<=r+dr<rows and 0<=c+dc<cols and grid[r+dr, c+dc] == self.FLOOR)
                    f_diag = sum(1 for dr, dc in [(-1,-1),(-1,1),(1,-1),(1,1)] if 0<=r+dr<rows and 0<=c+dc<cols and grid[r+dr, c+dc] == self.FLOOR)
                    if f_card >= 2: new_grid[r, c] = self.CORNER_INT
                    elif f_card == 1: new_grid[r, c] = self.WALL_STRAIGHT
                    elif f_diag > 0: new_grid[r, c] = self.CORNER_EXT
        return new_grid

    def clean_corner_neighbors(self, grid):
        rows, cols = grid.shape
        final_grid = grid.copy()
        for r in range(rows):
            for c in range(cols):
                if grid[r, c] == self.CORNER_INT:
                    for dr, dc in [(-1,0),(1,0),(0,-1),(0,1)]:
                        if 0<=r+dr<rows and 0<=c+dc<cols and grid[r+dr, c+dc] == self.WALL_STRAIGHT:
                            final_grid[r+dr, c+dc] = self.WALL_PLACEHOLDER
        return final_grid

    def generate_legal_hall(self, min_corner_dist=3, min_door_dist=15, safe_zone_radius=3):
        attempts = 0
        while attempts < 100:
            grid = np.zeros((25, 25), dtype=int)
            w1, h1 = random.randint(12, 18), random.randint(12, 18)
            x1, y1 = random.randint(2, 25-w1-2), random.randint(2, 25-h1-2)
            grid[y1 : y1 + h1, x1 : x1 + w1] = self.FLOOR
            for _ in range(random.randint(1, 3)):
                f_coords = np.argwhere(grid == self.FLOOR); anchor = random.choice(f_coords)
                w_ext, h_ext = random.randint(6, 12), random.randint(6, 12)
                x_ext = max(1, min(24-w_ext-1, anchor[1] - random.randint(0, w_ext-1)))
                y_ext = max(1, min(24-h_ext-1, anchor[0] - random.randint(0, h_ext-1)))
                grid[y_ext:y_ext+h_ext, x_ext:x_ext+w_ext] = self.FLOOR
            temp_grid = self.apply_smart_walls(grid)
            corners = np.argwhere((temp_grid == self.CORNER_EXT) | (temp_grid == self.CORNER_INT))
            corner_ok = True
            for i in range(len(corners)):
                for j in range(i + 1, len(corners)):
                    if self.get_distance(corners[i], corners[j]) < min_corner_dist: corner_ok = False; break
            if not corner_ok: attempts += 1; continue
            temp_grid = self.clean_corner_neighbors(temp_grid)
            walls = np.argwhere(temp_grid == self.WALL_STRAIGHT)
            if len(walls) >= 2:
                door_placed = False; d1_pos, d2_pos = None, None
                for _ in range(100):
                    idx1, idx2 = random.sample(range(len(walls)), 2); p1, p2 = walls[idx1], walls[idx2]
                    if self.get_distance(p1, p2) >= min_door_dist:
                        temp_grid[p1[0], p1[1]] = self.DOOR_IN; temp_grid[p2[0], p2[1]] = self.DOOR_OUT
                        d1_pos, d2_pos = p1, p2; door_placed = True; break
                if not door_placed: attempts += 1; continue
                safe_floor = [t for t in np.argwhere(temp_grid == self.FLOOR) if self.get_distance(t, d1_pos) >= safe_zone_radius and self.get_distance(t, d2_pos) >= safe_zone_radius]
                if safe_floor:
                    for t in random.sample(safe_floor, min(random.randint(1, 2), len(safe_floor))): temp_grid[t[0], t[1]] = self.ROBOT_ARM
                return temp_grid
            attempts += 1
        return np.zeros((25, 25))

gen = IndustrialShapeGenerator()

@app.get("/generate")
async def api_generate():
    matrix = gen.generate_legal_hall()
    path = gen.find_optimal_path(matrix)
    ai_sol, paid_conv, paid_rob, base_budget = gen.process_ai_solution(matrix, path)
    return {
        "width": 25, "height": 25, 
        "data": matrix.flatten().tolist(),
        "ai_solution": ai_sol.flatten().tolist(),
        "base_budget": float(base_budget),
        "paid_conveyors": paid_conv,
        "paid_robots": paid_rob
    }

@app.get("/visual_test")
async def visual_test():
    matrix = gen.generate_legal_hall()
    path = gen.find_optimal_path(matrix)
    ai_sol, paid_conv, paid_rob, base_budget = gen.process_ai_solution(matrix, path)
    
    img_data = np.zeros((25, 25, 3))
    for r in range(25):
        for c in range(25):
            if ai_sol[r, c] != 0: img_data[r, c] = gen.colors[ai_sol[r, c]]
            else: img_data[r, c] = gen.colors[matrix[r, c]]

    plt.figure(figsize=(8, 8))
    plt.imshow(img_data, interpolation='nearest')
    plt.title(f"Budget: {base_budget} Cr (+30%: {base_budget*1.3:.0f})\nPlatite -> Conv: {paid_conv} | Rob: {paid_rob}")
    plt.axis('off')
    buf = io.BytesIO()
    plt.savefig(buf, format='png')
    buf.seek(0)
    plt.close()
    return Response(content=buf.getvalue(), media_type="image/png")

if __name__ == "__main__":
    uvicorn.run(app, host="127.0.0.1", port=8000)