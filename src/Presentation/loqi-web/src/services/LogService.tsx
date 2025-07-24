import { LogSearchDto, ApiResponse, LogDto } from "../types/log";

class LogService {
    private baseUrl = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5003';

    async searchLogs(searchParams: LogSearchDto): Promise<ApiResponse<LogDto[]>> {
        try {
            const response = await fetch(`${this.baseUrl}/api/v1/log/search`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify(searchParams),
            });

            if (!response.ok) {
                const errorData = await response.json();
                throw new Error(errorData.error || `HTTP ${response.status}`);
            }

            return await response.json();
        } catch (error) {
            console.error('Log search error:', error);
            throw error;
        }
    }
}
export const logService = new LogService();