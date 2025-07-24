import { LogSearchDto, ApiResponse, LogDto, LogMetadata } from "../types/log";

class LogService {
    private baseUrl = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5003';

    async getLogMetadata(): Promise<ApiResponse<LogMetadata>> {
        try {
            const response = await fetch(`${this.baseUrl}/api/v1/log/metadata`, {
                method: 'GET',
                headers: {
                    'Content-Type': 'application/json',
                },
            });

            if (!response.ok) {
                const errorData = await response.json();
                throw new Error(errorData.error || `HTTP ${response.status}`);
            }

            return await response.json();
        } catch (error) {
            console.error('Log metadata fetch error:', error);
            throw error;
        }
    }

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

    async getLogByUniqueId(uniqueId: string): Promise<ApiResponse<LogDto>> {
        try {
            const response = await fetch(`${this.baseUrl}/api/v1/log/${uniqueId}`, {
                method: 'GET',
                headers: {
                    'Content-Type': 'application/json',
                },
            });

            if (!response.ok) {
                const errorData = await response.json();
                throw new Error(errorData.error || `HTTP ${response.status}`);
            }

            return await response.json();
        } catch (error) {
            console.error('Log detail fetch error:', error);
            throw error;
        }
    }
}

export const logService = new LogService();